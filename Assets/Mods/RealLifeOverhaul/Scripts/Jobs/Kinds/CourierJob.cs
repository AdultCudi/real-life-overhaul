using BigAmbitions.Items;
using Helpers;
using RealLifeOverhaul.GameUi;
using UnityEngine;
using Random = UnityEngine.Random;

namespace RealLifeOverhaul
{
    /// <summary>
    /// Parcel courier in the player's own car, or a van hired for the day (<see cref="JobRental"/>). Collect a round of
    /// parcels from a shop hand at a door nearby, load the car, then deliver to the round's doors in any order against the
    /// clock: park, take a parcel from the car, carry it to the door and hand it over. Paid per door and by the round's
    /// length, with a bonus for beating the clock and tips at some doors, less for crashes and police stops. Unlike the
    /// game's van job it needs no van and is over in minutes. The parcel is the game's own sealed box; the recipient carries
    /// it indoors when the clips allow, else it is set down on the step. The game's panel over a place
    /// (<see cref="JobPanel"/>) offers each step on the interact key; there is no clock to leave the round to, as the car
    /// would have to move.
    /// </summary>
    public sealed partial class CourierJob : Job
    {
        private enum Step { ToDepot, ToCar, Round }

        private const float PanelUp = 1.8f;          // metres over the door or the car
        private const string ButtonCollect = "reallife_job_courier_button_collect";
        private const string ButtonLoad = "reallife_job_courier_button_load";
        private const string ButtonTake = "reallife_job_courier_button_take";
        private const string ButtonHand = "reallife_job_courier_button_hand";
        private const string ButtonLeave = "reallife_job_courier_button_leave";

        private const int MaxDrops = 7;             // the most parcels an offer can make a round: the table's five and the offer's spread of two
        private const int MostExtra = 3;             // a senior courier's round is this much bigger at most
        private const float DepotMax = 240f;         // metres from the player
        private const float DropMin = 110f;          // metres from the depot
        private const float DropMax = 520f;          // metres from the depot
        private const float DropsApart = 30f;        // metres between two doors
        private const float LaneWithin = 30f;        // metres: a door on the round has a road this near, so there is somewhere to stop
        private const float PersonAppears = 90f;     // metres
        private const float PersonGoes = 60f;        // metres: someone at a door the player has left is taken in this far behind
        private const float HandReach = 2.5f;        // metres
        private const float CarReach = 1.5f;         // metres from the side of the car at which a parcel can be taken out or put in
        private const float DoorNearCar = 85f;       // metres: a parcel is only taken out where a door is this near
        private const float TipChance = 0.35f;       // a recipient's chance of tipping
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
        private ItemInstance _box;          // in the player's hands; null when it is in the car
        private bool _hired;                // the round is driven in a hired van
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
        private bool _wanted;           // the panel is wanted somewhere this tick
        private int _shownSeconds = int.MinValue;

        public CourierJob()
        {
            _panel = new JobPanel("courier panel", Act);
        }

        public override JobDef Def => _def;

        /// <summary>While the panel is up its first button has the interact key.</summary>
        public override bool OwnsInteractKey => _panel.Up;

        /// <summary>Giving up with most of a loaded round undelivered is a bad shift.</summary>
        public override bool QuitIsFailure => _step == Step.Round && _delivered * 2 < _count;

        protected override bool OnBegin(float now)
        {
            int want = ShiftSize() + (Ctx.Shift != null ? Mathf.Clamp(Ctx.Shift.Rank.ExtraUnits, 0, MostExtra) : 0);   // the offer's round, and a senior courier's extra
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

        // Doors in different buildings, not on top of one another; the round is as long as a sensible driver's order.
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
            // Nearest neighbour from the depot: near enough to what the player will drive.
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

        public override void Tick(float now)
        {
            bool onFoot = !Ctx.Driving && !PlayerHelper.IsUsingVehicle;
            if (_hired && Ctx.Rental.Car == null && JobDirector.NearestCar(60f) == null)
            {
                // The hired van is gone (wrecked and taken away by the game): nobody's bad shift, paid for the doors made.
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

        // The panel over a place, with one button on the interact key.
        private void Up(string first, string action, bool actionOn, Vector3 over)
        {
            _wanted = true;
            _panel.Offer(first, _panelLeft, JobText.Number(_count - _delivered), _panel.White, action, actionOn, null, false);
            _panel.Show(over, Ctx.Player);
        }

        // The round's figures on the card, each line written only when it changes.
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
                _clerkNext = Time.time + 3f;   // nobody could be put there: not asked again every tick
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
            ClerkGoes();
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

        // The shop hand goes in once the player is well away.
        private void ClerkGoes()
        {
            if (_clerk != null && JobSpots.Flat(Ctx.PlayerPosition, _clerk.Position) > PersonGoes)
            {
                Ctx.Cast.Release(_clerk);
                _clerk = null;
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
            ClerkGoes();
            if (_box != null && PlayerHelper.ItemInstanceInHands != _box)
            {
                _box = null;   // stowed by the game when the player got back in with it: in the car again
                ClearStowed();
            }

            // The nearest door still to do is pointed at; any of them may be done.
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
            // The line under the job's name: the round, or the one thing the player seems to have forgotten.
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
                // Over the person waiting at the door, or the door itself when nobody is there: the parcel is then left at it.
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

        // Someone waits at the door being headed for; if nobody can be put there, the parcel is left at the door.
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

        // The panel's button, run by the game on its key or a click.
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
            UnitDone(_delivered, UnitPay());   // the round's big tipper, if the offer drew one
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
                // With the mod's clip the parcel is theirs as the clip closes their hands on it, to the frame, and their arms settle
                // into carrying as it ends; without it, at once.
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
                    _tips += Mathf.RoundToInt(Random.Range(5.5f, 16.5f) * Ctx.Rate);
                }
                if (_delivered % 2 == 1)
                {
                    Ctx.Say(JobText.Pick(Thanks, ref _line), 5f);
                }
                // They take it indoors: a moment, then to the door, then through it.
                if (_goingIn != null)
                {
                    Ctx.Cast.Release(_goingIn);   // the one before; PassParcel has just put their parcel away
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

        // At the side or the back of the car, near enough to reach in.
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

        private void Conclude(float now, bool late)
        {
            bool all = _delivered >= _count;
            Settle(all && !late && _endsAt - now >= _window * 0.25f);
            Finish(all || _delivered * 2 >= _count ? JobEnd.Done : JobEnd.Failed);
        }

        private void Settle(bool beatTheClock)
        {
            if (!Owed(beatTheClock, out PaySlip slip))
            {
                return;
            }
            _paid = true;
            Ctx.Pay(this, slip, "reallife_job_courier_slip");
        }

        // A round that stops early has not beaten the clock.
        protected override bool Owed(out PaySlip slip) => Owed(false, out slip);

        private bool Owed(bool beatTheClock, out PaySlip slip)
        {
            slip = default;
            if (_paid || _delivered <= 0 || Ctx == null)
            {
                return false;
            }
            int each = UnitPay();
            slip = new PaySlip
            {
                Units = _delivered,
                Base = _delivered * each,
                Distance = DistancePay(_route * _delivered / _count),
                Tips = Mathf.RoundToInt(_tips * Ctx.TipFactor),
                Bonus = beatTheClock ? Mathf.RoundToInt(_delivered * each * 0.15f) : 0,
                // A crash shakes every parcel aboard and a stop holds the round up: each costs two doors' pay.
                Penalty = Mathf.RoundToInt(each * (_crashes * JobCatalogue.CrashCut + _stops * JobCatalogue.PoliceStopCut) * 2f)
            };
            return true;
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
