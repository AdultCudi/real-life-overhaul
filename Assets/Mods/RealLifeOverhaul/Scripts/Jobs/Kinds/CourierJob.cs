using System;
using BigAmbitions.Items;
using Characters.EmojiSystem;
using Helpers;
using RealLifeOverhaul.GameUi;
using UnityEngine;
using Vehicles.DeliveryDriverJob;
using Random = UnityEngine.Random;

namespace RealLifeOverhaul
{
    /// <summary>
    /// Parcel courier with the player's own car, or a van hired for the day if they have none
    /// near (JobRental). Collect a round of parcels from a shop hand
    /// at a door nearby, carry them to the car and load them; then to the doors on the round,
    /// in any order, against the clock: park, take a parcel from the car, carry it to the door
    /// and hand it to whoever is waiting there, who takes it inside. Paid per door and by the
    /// length of the round, with a bonus for beating the clock and tips at some doors; less for
    /// every crash on the way and every time the police pull the car over.
    ///
    /// It is not the game's delivery-van job (a company van, a hand truck and boxes of stock for
    /// shops): this one needs no van, is over in a few minutes and is done on foot at each end.
    ///
    /// Uses: the game's own carrying (a sealed cardboard box in both hands, the item its van
    /// job delivers, which cannot be opened, shelved or sold), the game's walk for the people
    /// at the doors, JobClip.HandOver, ReceiveBox, LoadBoot and Knock. What is handed over is
    /// seen to change hands: a parcel in the recipient's arms, carried indoors, when the clip
    /// bundle has the clips for holding and walking with one; set down on the step in front of
    /// them and gone with them through the door when it has not.
    ///
    /// IN THE GAME'S OWN INTERFACE. The game's panel over a place (<see cref="JobPanel"/>) stands over the parcels'
    /// door with "Collect the parcels", over the car with "Load the car" and "Take a parcel from the car", and over the
    /// door (or the person waiting at it) with "Hand over the parcel" or "Leave the parcel at the door", each on the
    /// game's interact key. The round's figures are on the HUD in a copy of the game's job panel
    /// (<see cref="ShiftFigures"/>). There is no "Let time pass": the car would have to be moved by the clock. The
    /// player works in their own clothes.
    /// </summary>
    public sealed class CourierJob : Job
    {
        private enum Step { ToDepot, ToCar, Round }

        private const float PanelUp = 1.8f;          // metres over the door or the car the panel stands
        private const string ButtonCollect = "reallife_job_courier_button_collect";
        private const string ButtonLoad = "reallife_job_courier_button_load";
        private const string ButtonTake = "reallife_job_courier_button_take";
        private const string ButtonHand = "reallife_job_courier_button_hand";
        private const string ButtonLeave = "reallife_job_courier_button_leave";

        private const string SealedBox = "ba:itemname_sealedcardboardbox";
        private const int MaxDrops = 5;
        private const int MostExtra = 3;             // a senior courier's round is this much bigger at most
        private const float DepotMax = 240f;
        private const float DropMin = 110f;
        private const float DropMax = 520f;
        private const float DropsApart = 30f;
        private const float LaneWithin = 30f;        // a door on the round has a road this near: there is somewhere to stop
        private const float PersonAppears = 90f;
        private const float PersonGoes = 60f;        // someone at a door the player has left is taken in once this far behind
        private const float HandReach = 2.5f;
        private const float CarReach = 1.5f;         // metres from the side of the car at which a parcel can be taken out or put in
        private const float DoorNearCar = 85f;       // a parcel is only taken out where there is a door this near to carry it to
        private const float TipChance = 0.35f;
        private const string HoldClip = "carry_box_hold";    // the arms alone, over the game's own walk and idle: nothing of the legs is ours, nothing can slide
        private const string CarryClip = "carry_box_walk";   // the whole body walking, where a bundle has no such arms-only clip: kept in step with the ground
        private const float HoldLead = 0.3f;                 // seconds before the end of taking a parcel at which the arms settle into carrying it

        // The clips a parcel is held with, best first: each brings its own prop and grip (the same grip: they share the box).
        private static readonly string[] ParcelGrips = { "job_receive_box", "carry_box_hold", "carry_box_walk" };

        private static readonly string[] Thanks =
        {
            "reallife_job_courier_thanks_1", "reallife_job_courier_thanks_2", "reallife_job_courier_thanks_3", "reallife_job_courier_thanks_4"
        };
        private static readonly string[] Bumps = { "reallife_job_courier_bump_1", "reallife_job_courier_bump_2" };

        private struct Drop
        {
            public DoorSpot Spot;
            public bool Done;
            public bool NoOne;      // nobody could be put at this door: the parcel is left at it
        }

        private readonly JobDef _def = JobCatalogue.Find(JobCatalogue.Courier);
        private readonly Drop[] _drops = new Drop[MaxDrops + MostExtra];

        private Step _step;
        private DoorSpot _depot;
        private int _count;
        private int _delivered;
        private float _route;
        private float _window;
        private float _endsAt;
        private StreetActor _clerk;         // the shop hand at the depot door
        private float _clerkNext;
        private StreetActor _recipient;     // whoever waits at the door the player is heading for
        private int _recipientFor = -1;
        private float _recipientTried = -1f;
        private StreetActor _goingIn;       // the last recipient, carrying the parcel indoors
        private float _goingAt;
        private float _goingUntil;
        private bool _goingWalks;
        private Vector3 _goingTo;
        private ItemInstance _box;          // the parcel in the player's hands; null when it is in the car
        private bool _hired;                // the round is driven in a hired van
        private readonly HeldProp _handed = new HeldProp();   // the parcel in the last recipient's arms
        private Transform _onStep;          // ...or on the step in front of them
        private ActivityAnimator _carrying; // the recipient with a box in their arms: the clip player that carries it...
        private bool _carryingOver;         // ...on the masked layer (arms only), not as a whole-body walk
        private float _carryMade;           // m/s that whole-body walk was made for
        private StreetActor _passTo;        // whoever is reaching for the parcel: it is theirs at the clip's "grip"...
        private float _passHoldAt = -1f;    // ...and their arms settle into carrying it at this time
        private int _target = -1;
        private int _shownTarget = -2;
        private int _shownDelivered = -1;
        private int _hint = -1;
        private int _crashes;
        private int _stops;
        private int _tips;
        private bool _paid;
        private int _line = -1;
        private string _depotLabel;
        private string _carLabel;
        private string _doorLabel;

        // The game's own interface.
        private readonly JobPanel _panel;
        private ShiftFigures _figures;
        private string _panelDepot;
        private string _panelHands;
        private string _panelCar;
        private string _panelLeft;
        private bool _wanted;           // the panel is wanted somewhere on this tick
        private int _shownSeconds = int.MinValue;

        public CourierJob()
        {
            _panel = new JobPanel("courier panel", Act);
        }

        public override JobDef Def => _def;

        /// <summary>While the panel is up its first button is on the game's interact key: the mod's own use of that key stands aside.</summary>
        public override bool OwnsInteractKey => _panel.Up;

        /// <summary>Giving up with most of a loaded round undelivered is a bad shift.</summary>
        public override bool QuitIsFailure => _step == Step.Round && _delivered * 2 < _count;

        protected override bool OnBegin(float now)
        {
            int want = Ctx.FullShift ? MaxDrops + Mathf.Clamp(Ctx.Shift != null ? Ctx.Shift.Rank.ExtraUnits : 0, 0, MostExtra) : Mathf.Min(MaxDrops, JobState.LeftToday(Def));
            if (want <= 0)
            {
                Refusal = "reallife_job_no_daily";
                return false;
            }
            if (!JobSpots.RandomDoor(Ctx.PlayerPosition, 12f, DepotMax, null, LaneWithin, out _depot) || !PlanRound(want))
            {
                Refusal = "reallife_job_no_here";
                return false;
            }
            // Last of all, once nothing else can refuse: the player's own car, or a van hired for the day.
            CarController wheels = Ctx.Director.CarFor(Ctx.Test || Ctx.Perk(CareerPerk.CompanyVan), out string noCar);   // the firm's van costs a senior courier nothing
            if (wheels == null)
            {
                Refusal = noCar ?? "reallife_job_no_car";
                return false;
            }
            _hired = ReferenceEquals(Ctx.Rental.Car, wheels);
            _depotLabel = JobText.T("reallife_job_courier_marker_depot");
            _carLabel = JobText.T("reallife_job_courier_marker_car");
            _doorLabel = JobText.T("reallife_job_courier_marker_door");
            _panelDepot = JobText.T("reallife_job_courier_panel_depot", "place", _depot.Name);
            _panelHands = JobText.T("reallife_job_obj_hands");
            _panelCar = JobText.T("reallife_job_courier_panel_car");
            _panelLeft = JobText.T("reallife_job_courier_panel_left");
            _step = Step.ToDepot;
            _figures = new ShiftFigures(Ctx, Def, "reallife_job_courier_card_done");
            Show(JobText.T("reallife_job_courier_obj_depot", "count", _count.ToString(), "place", _depot.Name));
            Mark(_depot.Front, _depotLabel);
            Figures(now);
            Ctx.Logger?.Info("Jobs: courier round of " + _count + " from " + _depot.Name + ", " + _route.ToString("F0") + " m, " + _window.ToString("F0") + " s.");
            if (Meta.Dev.Verbose)
            {
                Ctx.Logger?.Info("Jobs: courier: the depot is " + JobSpots.Flat(Ctx.PlayerPosition, _depot.Front).ToString("F0") + " m from the player, " + (_hired ? "in a hired van" : "in the player's own car")
                                 + "; shown in the game's own interface: job panel " + (_figures.Shown ? "copied" : "NOT copied (the panels over the places are all there is)") + ", no clock offered (the car would have to be moved).");
            }
            return true;
        }

        // Doors for the round: different buildings, not on top of one another. The round is as long as the order a sensible driver would take.
        private bool PlanRound(int want)
        {
            _count = 0;
            for (int attempt = 0; attempt < want * 6 && _count < want; attempt++)
            {
                if (!JobSpots.RandomDoor(_depot.Front, DropMin, DropMax, _depot.Building, LaneWithin, out DoorSpot spot))
                {
                    continue;
                }
                bool apart = true;
                for (int i = 0; i < _count; i++)
                {
                    if (_drops[i].Spot.Building == spot.Building || JobSpots.Flat(_drops[i].Spot.Front, spot.Front) < DropsApart)
                    {
                        apart = false;
                        break;
                    }
                }
                if (apart)
                {
                    _drops[_count++] = new Drop { Spot = spot };
                }
            }
            if (_count < Mathf.Min(2, want))   // a round of one only when one is all that is left of the day
            {
                return false;
            }
            // Nearest-neighbour from the depot: near enough to what the player will drive.
            _route = 0f;
            Vector3 at = _depot.Front;
            int visited = 0;
            for (int leg = 0; leg < _count; leg++)
            {
                int next = -1;
                float best = float.MaxValue;
                for (int i = 0; i < _count; i++)
                {
                    float d = JobSpots.Flat(at, _drops[i].Spot.Front);
                    if ((visited & (1 << i)) == 0 && d < best)
                    {
                        best = d;
                        next = i;
                    }
                }
                visited |= 1 << next;
                _route += best;
                at = _drops[next].Spot.Front;
            }
            _window = 70f + _route / 4.5f + 38f * _count;
            return true;
        }

        // ---------------------------------------------------------------- the tick

        public override void Tick(float now)
        {
            bool onFoot = !Ctx.Driving && !PlayerHelper.IsUsingVehicle;
            if (_hired && Ctx.Rental.Car == null && JobDirector.NearestCar(60f) == null)
            {
                // The hired van is gone (wrecked, and taken away by the game): nobody's bad shift, paid for the doors made.
                Ctx.Say("reallife_job_rental_lost", 9f);
                Finish(JobEnd.Interrupted);
                return;
            }
            SeeIn(now);
            _wanted = false;
            switch (_step)
            {
                case Step.ToDepot: ToDepot(onFoot); break;
                case Step.ToCar: ToCar(now, onFoot); break;
                case Step.Round: Round(now, onFoot); break;
            }
            if (!_wanted)
            {
                _panel.Hide();
            }
            if (!Over)
            {
                Figures(now);
            }
        }

        // The panel over a place, with one button on the game's interact key.
        private void Up(string first, string action, bool actionOn, Vector3 over)
        {
            _wanted = true;
            _panel.Offer(first, _panelLeft, JobText.Number(_count - _delivered), _panel.White, action, actionOn, null, false);
            _panel.Show(over, Ctx.Player);
        }

        // The round's figures on the card: each line is written only when what it says changes.
        private void Figures(float now)
        {
            _figures.Objective(Objective);
            _figures.Units(_delivered, _count);
            int seconds = _step == Step.Round ? Mathf.Max(0, Mathf.CeilToInt(_endsAt - now)) : -1;
            if (seconds != _shownSeconds)
            {
                _shownSeconds = seconds;
                _figures.Limit(seconds < 0 ? null : JobText.T("reallife_job_card_time_left", "time", ShiftFigures.Clock(seconds)), seconds >= 0 && seconds <= 60 ? WorldPanel.Yellow() : _panel.White);
            }
            _figures.Money(_delivered * UnitPay() + (_count > 0 ? DistancePay(_route * _delivered / _count) : 0), Mathf.RoundToInt(_tips * Ctx.TipFactor));
            _figures.Time(-1);
        }

        private void ToDepot(bool onFoot)
        {
            float away = JobSpots.Flat(Ctx.PlayerPosition, _depot.Front);
            if (_clerk == null && away < PersonAppears && Time.time >= _clerkNext)
            {
                _clerkNext = Time.time + 3f;   // if nobody can be put there, not asked again every tick
                _clerk = Ctx.Cast.Put(_depot.Front, false);
                if (_clerk != null)
                {
                    _clerk.StandAt(_depot.Front, Ctx.PlayerPosition);
                }
            }
            if (_clerk != null && away < 25f)
            {
                _clerk.Face(Ctx.PlayerPosition);
            }
            bool reach = onFoot && away < HandReach;
            bool handsFull = PlayerHelper.IsHoldingItem;
            Hint(reach && handsFull ? 1 : 0, "reallife_job_courier_obj_hands", null);
            if (reach)
            {
                Up(handsFull ? _panelHands : _panelDepot, ButtonCollect, !handsFull, _depot.Front + Vector3.up * PanelUp);
            }
        }

        private void ToCar(float now, bool onFoot)
        {
            if (_clerk != null && JobSpots.Flat(Ctx.PlayerPosition, _clerk.Position) > PersonGoes)
            {
                Ctx.Cast.Release(_clerk);
                _clerk = null;
            }
            if (_box == null || PlayerHelper.ItemInstanceInHands != _box)
            {
                // Out of the player's hands by the game's own means (getting into the car stows it): that is loading too.
                _box = null;
                ClearStowed();
                Loaded(now);
                return;
            }
            CarController car = JobDirector.NearestCar(250f);
            if (car != null)
            {
                Mark(car.transform.position, _carLabel);
            }
            else
            {
                Unmark();
            }
            if (onFoot && car != null && BesideCar(car))
            {
                Up(_panelCar, ButtonLoad, true, car.transform.position + Vector3.up * PanelUp);
            }
        }

        private void Loaded(float now)
        {
            _endsAt = now + _window;
            _step = Step.Round;
            _hint = -1;
            _shownTarget = -2;
            Ctx.Say("reallife_job_courier_loaded", 6f);
        }

        private void Round(float now, bool onFoot)
        {
            if (now >= _endsAt)
            {
                Ctx.Say("reallife_job_courier_time", 8f);
                Conclude(now, true);
                return;
            }
            if (_clerk != null && JobSpots.Flat(Ctx.PlayerPosition, _clerk.Position) > PersonGoes)
            {
                Ctx.Cast.Release(_clerk);
                _clerk = null;
            }
            if (_box != null && PlayerHelper.ItemInstanceInHands != _box)
            {
                _box = null;   // stowed by the game (the player got back in with it): it is "in the car" again
                ClearStowed();
            }

            // The nearest door still to do is the one pointed at; any of them may be done.
            Vector3 player = Ctx.PlayerPosition;
            _target = -1;
            float best = float.MaxValue;
            for (int i = 0; i < _count; i++)
            {
                if (_drops[i].Done)
                {
                    continue;
                }
                float d = JobSpots.Flat(player, _drops[i].Spot.Front);
                if (d < best)
                {
                    best = d;
                    _target = i;
                }
            }
            if (_target < 0)
            {
                Conclude(now, false);
                return;
            }
            Vector3 door = _drops[_target].Spot.Front;
            if (_target != _shownTarget || _delivered != _shownDelivered)
            {
                _shownTarget = _target;
                _shownDelivered = _delivered;
                _hint = -1;
                Mark(door, _doorLabel);
            }
            Recipient(now, best);

            bool holding = _box != null;
            bool atDoor = onFoot && best < HandReach;
            // What the line under the job's name says: the round, or the one thing the player seems to have forgotten.
            if (onFoot && !holding && best < 30f)
            {
                Hint(2, "reallife_job_courier_obj_fetch", null);
            }
            else
            {
                Hint(3 + _target * 8 + _delivered, "reallife_job_courier_obj_round", _drops[_target].Spot.Name);
            }

            if (holding && atDoor)
            {
                // Over the person waiting at the door, or the door itself when nobody is: then the parcel is left there.
                bool someone = _recipient != null && _recipientFor == _target;
                Up(_drops[_target].Spot.Name, someone ? ButtonHand : ButtonLeave, true, (someone ? _recipient.Position : door) + Vector3.up * PanelUp);
                return;
            }
            if (!holding && onFoot && !PlayerHelper.IsHoldingItem)
            {
                CarController car = JobDirector.NearestCar(8f);
                if (car != null && BesideCar(car) && DoorNear(car.transform.position))
                {
                    Up(_panelCar, ButtonTake, true, car.transform.position + Vector3.up * PanelUp);
                }
            }
        }

        // Someone waits at the door being headed for, once the player is in the street. If nobody can be put there, the parcel is left at the door.
        private void Recipient(float now, float away)
        {
            if (_recipient != null && _recipientFor != _target
                && (JobSpots.Flat(Ctx.PlayerPosition, _recipient.Position) > PersonGoes || !_recipient.Visible || away < 40f))
            {
                Ctx.Cast.Release(_recipient);
                _recipient = null;
                _recipientFor = -1;
            }
            if (_recipient != null || away > PersonAppears || _drops[_target].NoOne)
            {
                return;
            }
            Vector3 front = _drops[_target].Spot.Front;
            _recipient = Ctx.Cast.Put(front, false);
            if (_recipient != null)
            {
                _recipient.StandAt(front, Ctx.PlayerPosition);
                _recipientFor = _target;
                _recipientTried = -1f;
                return;
            }
            if (_recipientTried < 0f)
            {
                _recipientTried = now;
            }
            else if (now - _recipientTried > 4f)
            {
                _recipientTried = -1f;
                _drops[_target].NoOne = true;
            }
        }

        // ---------------------------------------------------------------- the key

        // The panel's button was pressed (the game ran it on its key or on a click).
        private void Act(string action)
        {
            if (Ctx == null || Over)
            {
                return;
            }
            float now = Time.time;
            if (ReferenceEquals(action, ButtonCollect) && _step == Step.ToDepot && !PlayerHelper.IsHoldingItem)
            {
                if (_clerk != null)
                {
                    Ctx.Animations.Play(_clerk.Character, JobClip.HandOver);
                }
                if (GiveBox())
                {
                    _step = Step.ToCar;
                    _hint = -1;
                    Show(JobText.T("reallife_job_courier_obj_car"));
                    if (Meta.Dev.Verbose)
                    {
                        Ctx.Logger?.Info("Jobs: courier: the parcels are in the player's hands; the car is next.");
                    }
                }
            }
            else if (ReferenceEquals(action, ButtonLoad) && _step == Step.ToCar)
            {
                TakeBox();
                CarController car = JobDirector.NearestCar(12f);
                if (car != null)
                {
                    Ctx.TurnPlayer(car.transform.position);
                }
                float lower = Ctx.Animations.Play(Ctx.Player.Character, JobClip.LoadBoot);
                if (lower > 0f)
                {
                    Ctx.Self.Hold(lower);   // bent over the boot: not walking off meanwhile
                }
                Loaded(now);
            }
            else if (ReferenceEquals(action, ButtonTake) && _step == Step.Round && _box == null)
            {
                GiveBox();
            }
            else if ((ReferenceEquals(action, ButtonHand) || ReferenceEquals(action, ButtonLeave)) && _step == Step.Round && _box != null && _target >= 0 && !_drops[_target].Done)
            {
                Deliver(now, ReferenceEquals(action, ButtonHand));
            }
        }

        private void Deliver(float now, bool toSomeone)
        {
            TakeBox();   // out of the player's hands first, so the arm is free to reach
            _drops[_target].Done = true;
            _delivered++;
            if (Meta.Dev.Verbose)
            {
                Ctx.Logger?.Info("Jobs: courier: parcel " + _delivered + " of " + _count + " " + (toSomeone && _recipient != null ? "handed to someone at the door" : "left at the door") + " (" + _drops[_target].Spot.Name + "); "
                                 + Mathf.Max(0, Mathf.CeilToInt(_endsAt - now)) + " s of the round left.");
            }
            Ctx.TurnPlayer(toSomeone && _recipient != null ? _recipient.Position : _drops[_target].Spot.Door);
            if (toSomeone && _recipient != null)
            {
                float reach = Ctx.Animations.Play(Ctx.Player.Character, JobClip.HandOver);
                if (reach > 0f)
                {
                    Ctx.Self.Hold(Mathf.Min(reach, 1.4f));
                }
                // With the mod's clip the parcel is in their arms from the moment the clip closes their hands on it
                // ("grip"), to the frame, and the arms settle into carrying as the clip ends; without, at once, as before.
                float grip = Ctx.Animations.Moment(JobClip.ReceiveBox, "grip");
                float taking = Ctx.Animations.Play(_recipient.Character, JobClip.ReceiveBox);
                if (grip >= 0f && taking > grip)
                {
                    DropParcel();
                    _passTo = _recipient;
                    _passHoldAt = now + Mathf.Max(grip + 0.1f, taking - HoldLead);
                    Ctx.At(now + grip);
                }
                else
                {
                    PassParcel(_recipient);
                }
                Ctx.Cast.Emoji(_recipient, CharacterEmojiName.CommonGreenHappy, 2f);
                if (Random.value < TipChance)
                {
                    _tips += Mathf.RoundToInt(Random.Range(2f, 6f) * Ctx.Rate);
                }
                if (_delivered % 2 == 1)
                {
                    Ctx.Say(JobText.Pick(Thanks, ref _line), 5f);
                }
                // They take it indoors: a moment, then to the door, then gone through it.
                if (_goingIn != null)
                {
                    Ctx.Cast.Release(_goingIn);   // the one before; their parcel was put away by PassParcel just now
                }
                _goingIn = _recipient;
                _recipient = null;
                _recipientFor = -1;
                _goingWalks = false;
                _goingAt = now + 1.4f;
                _goingUntil = now + 14f;
                if (!StreetSpots.OnRoad(_drops[_target].Spot.Door, 2.5f, out _goingTo))
                {
                    _goingTo = _goingIn.Position;
                }
            }
            else
            {
                Ctx.Animations.Play(Ctx.Player.Character, JobClip.Knock);
                Ctx.Say("reallife_job_courier_left", 5f);
            }
        }

        // The parcel leaves the player's hands and is seen to arrive: in the recipient's arms if the clip bundle
        // knows how one is held and walked with, otherwise on the step between them (where the player set it down).
        private void PassParcel(StreetActor to)
        {
            DropParcel();
            if ((ClipLibrary.Has(HoldClip) || ClipLibrary.Has(CarryClip)) && _handed.Give(to.Character, Ctx.Props, ParcelGrips))
            {
                return;
            }
            Vector3 between = Vector3.Lerp(to.Position, Ctx.Player.transform.position, 0.4f);
            if (StreetSpots.Ground(between, out float ground) && Mathf.Abs(ground - to.Position.y) < 0.4f)
            {
                between.y = ground;
                _onStep = Ctx.Props.Stand("parcel_m", between, to.Position - between);
            }
        }

        /// <summary>The two moments of a parcel being taken: the hands close on it, and then the arms settle into carrying it.</summary>
        public override void Moment(float now)
        {
            StreetActor to = _passTo;
            if (to != null)
            {
                _passTo = null;
                if (to.IsValid && to.InUse)
                {
                    PassParcel(to);
                    if (_handed.Held && _passHoldAt >= 0f)
                    {
                        Ctx.At(_passHoldAt);
                        return;
                    }
                }
                _passHoldAt = -1f;
                return;
            }
            if (_passHoldAt >= 0f)
            {
                _passHoldAt = -1f;
                StreetActor holder = _goingIn;
                if (holder != null && holder.IsValid && _handed.Held && _carrying == null)
                {
                    Carry(holder, false);
                }
            }
        }

        // A box in someone's arms, standing or walking: the arms-only clip over the game's own animation; where a bundle
        // has only the whole-body walk, that (and only once they walk), kept in step with the ground by SeeIn.
        private void Carry(StreetActor who, bool walking)
        {
            ActivityAnimator player = ActivityAnimators.Working && who.Character != null ? ActivityAnimators.For(who.Character.animator) : null;
            if (player == null || !player.Usable)
            {
                return;
            }
            if (ClipLibrary.Has(HoldClip) && ClipLibrary.TryGet(HoldClip, out CityClip hold) && hold.Loop)
            {
                if (player.PlayOver(hold.Clip, BodyMask.Arms, 0.3f))
                {
                    _carrying = player;
                    _carryingOver = true;
                }
            }
            else if (walking && ClipLibrary.Has(CarryClip) && ClipLibrary.TryGet(CarryClip, out CityClip carry) && carry.Loop && player.Play(carry.Clip, 0.3f))
            {
                _carrying = player;
                _carryingOver = false;
                _carryMade = carry.Speed;
            }
        }

        // Whatever showed the last parcel goes back on the shelf: it went indoors with whoever took it.
        private void DropParcel()
        {
            _passTo = null;
            _passHoldAt = -1f;
            if (_carrying != null)
            {
                if (_carryingOver)
                {
                    _carrying.StopOver(0.2f);
                }
                else
                {
                    _carrying.Stop(0.2f);
                }
                _carrying = null;
            }
            _handed.Take();
            if (_onStep != null)
            {
                Ctx.Props.Return(_onStep);
                _onStep = null;
            }
        }

        // The last recipient walks the parcel to their door and is gone.
        private void SeeIn(float now)
        {
            if (_goingIn == null)
            {
                return;
            }
            if (!_goingIn.IsValid)
            {
                DropParcel();
                _goingIn = null;
                return;
            }
            _handed.Show(_goingIn.Visible);
            if (!_goingWalks)
            {
                if (now < _goingAt)
                {
                    return;
                }
                _goingWalks = true;
                if (JobSpots.Flat(_goingIn.Position, _goingTo) > 0.8f && _goingIn.MoveTo(_goingTo, Pace.Walk))
                {
                    // Walking with a box in their arms: the arms they already carry it in go on over the game's walk.
                    if (_handed.Held && _carrying == null)
                    {
                        Carry(_goingIn, true);
                    }
                    return;
                }
                _goingUntil = now + 2.5f;   // already at the door: a moment more, then in
                return;
            }
            if (_carrying != null && !_carryingOver && _carryMade > 0f)
            {
                // A whole-body walking clip is played at the speed they are really going, so the planted foot does not slide.
                UnityEngine.AI.NavMeshAgent agent = _goingIn.Character.navmeshAgent;
                _carrying.MatchSpeed(agent != null && agent.enabled ? agent.velocity.magnitude : 0f, _carryMade);
            }
            bool there = now - _goingAt > 1.2f && _goingIn.HasArrived && JobSpots.Flat(_goingIn.Position, _goingTo) < 1.2f;
            if (there || now > _goingUntil)
            {
                DropParcel();
                Ctx.Cast.Release(_goingIn);
                _goingIn = null;
            }
        }

        // ---------------------------------------------------------------- the parcel in the player's hands

        // The game's own carried box: the sealed one its delivery job uses, which cannot be opened, shelved or sold.
        private bool GiveBox()
        {
            if (PlayerHelper.IsHoldingItem)
            {
                return false;
            }
            try
            {
                ItemInstance box = ItemHelper.InitializeItemInHandsWithCargo(new CargoInstance(SealedBox, 1, 0f));
                _box = box;   // known before it is in the hands, so it is taken back even if putting it there faults half way
                PlayerHelper.ItemInstanceInHands = box;
                return true;
            }
            catch (Exception e)
            {
                TakeBox();
                Ctx.Logger?.Error(e);
                Ctx.Logger?.Warn("Jobs: the game would not put a parcel in the player's hands; the courier round is called off.");
                Ctx.Say("reallife_job_courier_no_box", 9f);
                Finish(JobEnd.Interrupted);
                return false;
            }
        }

        private void TakeBox()
        {
            ItemInstance box = _box;
            _box = null;
            try
            {
                if (box != null && PlayerHelper.ItemInstanceInHands == box)
                {
                    PlayerHelper.ItemInstanceInHands = null;
                }
            }
            catch (Exception e)
            {
                Ctx.Logger?.Error(e);
            }
        }

        // The parcels "in the car" are a count, not cargo: a box the game stowed in the car's cargo (it does that when
        // the player gets in holding one) is taken out again, with the game's own clean-up for sealed boxes. Never
        // while one of the game's own deliveries is on: those boxes are its.
        private void ClearStowed()
        {
            try
            {
                if (SaveGameManager.Current != null && SaveGameManager.Current.currentPlayerMission == null)
                {
                    DeliveryJobHelper.DiscardSealedBoxes();
                }
            }
            catch (Exception e)
            {
                Ctx.Logger?.Error(e);
            }
        }

        /// <summary>
        /// After a load: a job in progress is never saved, but what the player was holding is. A sealed parcel
        /// still in their hands (it cannot be put down or sold) is taken away, and any in their cars with it.
        /// Never while one of the game's own deliveries is on: those boxes are its.
        /// </summary>
        public static void ClearLeftovers(BAModAPI.IModLogger logger)
        {
            try
            {
                if (SaveGameManager.Current == null || SaveGameManager.Current.currentPlayerMission != null)
                {
                    return;
                }
                ItemInstance held = PlayerHelper.ItemInstanceInHands;
                if (held != null)
                {
                    System.Collections.Generic.List<CargoInstance> cargo = held.GetCargoInstances();
                    for (int i = 0; cargo != null && i < cargo.Count; i++)
                    {
                        if (cargo[i] != null && cargo[i].IsSealed)
                        {
                            PlayerHelper.ItemInstanceInHands = null;
                            logger?.Info("Jobs: a parcel left in the player's hands by a saved courier round was taken away.");
                            break;
                        }
                    }
                }
                DeliveryJobHelper.DiscardSealedBoxes();
            }
            catch (Exception e)
            {
                logger?.Error(e);
            }
        }

        // ---------------------------------------------------------------- small questions

        // Standing at the side or the back of the car, near enough to reach in.
        private bool BesideCar(CarController car)
        {
            JobSpots.CarFrame(car, out Vector3 centre, out Vector3 forward, out float halfLength, out float halfWidth);
            Vector3 offset = Ctx.Player.transform.position - centre;
            offset.y = 0f;
            float along = Mathf.Abs(Vector3.Dot(offset, forward)) - halfLength;
            float across = Mathf.Abs(Vector3.Dot(offset, Vector3.Cross(Vector3.up, forward))) - halfWidth;
            return Mathf.Max(along, across) < CarReach;
        }

        private bool DoorNear(Vector3 car)
        {
            for (int i = 0; i < _count; i++)
            {
                if (!_drops[i].Done && JobSpots.Flat(car, _drops[i].Spot.Front) < DoorNearCar)
                {
                    return true;
                }
            }
            return false;
        }

        // The objective line, rebuilt only when what it says changes.
        private void Hint(int which, string key, string place)
        {
            if (which == _hint)
            {
                return;
            }
            _hint = which;
            if (which == 0)
            {
                Show(JobText.T("reallife_job_courier_obj_depot", "count", _count.ToString(), "place", _depot.Name));
            }
            else if (place != null)
            {
                Show(JobText.T(key, "done", _delivered.ToString(), "count", _count.ToString()).Replace("{place}", place));
            }
            else
            {
                Show(JobText.T(key));
            }
        }

        public override void OnCrash(float speed)
        {
            if (_step == Step.Round)
            {
                _crashes++;
                Ctx.Say(JobText.Pick(Bumps, ref _line), 5f);
            }
        }

        public override void OnPoliceStop()
        {
            if (_step == Step.Round)
            {
                _stops++;
            }
        }

        // ---------------------------------------------------------------- the end

        private void Conclude(float now, bool late)
        {
            bool all = _delivered >= _count;
            Settle(all && !late && _endsAt - now >= _window * 0.25f);
            Finish(all || _delivered * 2 >= _count ? JobEnd.Done : JobEnd.Failed);
        }

        private void Settle(bool beatTheClock)
        {
            if (_paid || _delivered <= 0 || Ctx == null)
            {
                return;
            }
            _paid = true;
            int each = UnitPay();
            var slip = new PaySlip
            {
                Units = _delivered,
                Base = _delivered * each,
                Distance = DistancePay(_route * _delivered / _count),
                Tips = Mathf.RoundToInt(_tips * Ctx.TipFactor),
                Bonus = beatTheClock ? Mathf.RoundToInt(_delivered * each * 0.15f) : 0,
                // A crash shakes every parcel aboard, a stop holds the whole round up: each is counted against two doors' pay.
                Penalty = Mathf.RoundToInt(each * (_crashes * JobCatalogue.CrashCut + _stops * JobCatalogue.PoliceStopCut) * 2f)
            };
            Ctx.Pay(this, slip, "reallife_job_courier_slip");
        }

        public override void End(JobEnd why)
        {
            if (Ctx == null)
            {
                return;
            }
            _panel.Hide();
            DropParcel();
            Ctx.Cast.Release(_clerk);
            Ctx.Cast.Release(_recipient);
            Ctx.Cast.Release(_goingIn);
            _clerk = null;
            _recipient = null;
            _goingIn = null;
            TakeBox();
            if (_step != Step.ToDepot)
            {
                ClearStowed();
            }
            if (why != JobEnd.Fault)
            {
                Settle(false);
            }
        }
    }
}
