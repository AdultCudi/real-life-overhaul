using Extensions;
using Helpers;
using RealLifeOverhaul.GameUi;
using UnityEngine;
using UnityEngine.AI;

namespace RealLifeOverhaul
{
    /// <summary>
    /// Shelf stocker: carry a delivery's boxes (the game's own carry) from the pavement into a shop near the player and on to
    /// its real shelves, then back for the next. Paid per box, a little more for clearing the pile in time; nothing is added
    /// to the shop's stock. At the pile the rest can be left to the game's clock (<see cref="ShiftClock"/>).
    /// </summary>
    public sealed class StockerJob : Job, IClockedShift
    {
        private enum Step { ToPile, CarryIn, ToShelf, Stocking }

        private const float Debounce = 0.7f;      // seconds after a box is shelved in which Let time pass does nothing
        private const float PanelUp = 1.6f;       // metres over the place
        private const string ButtonPick = "reallife_job_stocker_button_pick";
        private const string ButtonEnter = "reallife_job_button_enter";
        private const string ButtonStock = "reallife_job_stocker_button_stock";
        private const string ButtonSkip = "reallife_job_button_skip";

        private const int MaxBoxes = 8;            // the most a delivery can be: the table's six and the offer's spread of two
        private const float ClientMin = 15f;       // metres from the player
        private const float ClientMax = 260f;         // metres
        private const float DoorReach = 3.2f;         // metres
        private const float PileReach = 2.2f;         // metres
        private const float ShelfReach = 1.3f;        // metres
        private const float ShelvesApart = 1.8f;      // metres between two shelves stocked
        private const float Seconds = 300f;           // seconds for the delivery, the walk there included
        private const float SecondsPerMetre = 0.5f;   // seconds a metre more for a shop further off
        private const string BoxProp = "shelf_box";

        // Where each box of the pile sits, in the pile's own space: three on the ground, two on those, one on top.
        private static readonly Vector3[] PilePlaces =
        {
            new Vector3(-0.44f, 0f, 0f), new Vector3(0f, 0f, 0f), new Vector3(0.44f, 0f, 0f),
            new Vector3(-0.22f, 0.30f, 0f), new Vector3(0.22f, 0.30f, 0f), new Vector3(0f, 0.60f, 0f)
        };

        private readonly JobDef _def = JobCatalogue.Find(JobCatalogue.Stocker);
        private readonly Transform[] _pile = new Transform[MaxBoxes];
        private readonly Transform[] _shelved = new Transform[MaxBoxes];
        private readonly Vector3[] _shelves = new Vector3[MaxBoxes];

        private JobCarry _carry;
        private Step _step;
        private DoorSpot _client;
        private string _clientName;
        private Vector3 _pileAt;
        private int _boxes;            // how many the shift is
        private int _left;             // still on the pile
        private int _stocked;          // by hand and by the clock
        private int _hand;             // ...of which by hand
        private int _clockPay;         // what the clock's boxes were paid
        private float _endsAt;
        private float _shelvedAt = -10f;
        private readonly JobPanel _panel;
        private ShiftFigures _figures;
        private ShiftClock _clock;
        private string _panelPile;
        private string _panelHands;
        private string _panelDoor;
        private string _panelShelf;
        private string _panelLeft;
        private string _objClock;
        private int _shownSeconds = int.MinValue;
        private bool _wanted;          // the panel is wanted somewhere this tick
        private Vector3 _shelfStand;
        private Vector3 _shelfAt;
        private bool _hasShelf;
        private float _nextTry;
        private int _tries;
        private float _stockEnds;
        private bool _paid;
        private int _hint = -1;
        private string _pileLabel;
        private string _doorLabel;
        private string _shelfLabel;

        public StockerJob()
        {
            _panel = new JobPanel("stocker panel", Act, OfferSkip);
        }

        public override JobDef Def => _def;

        public override bool PassingTime => _clock != null && _clock.Working;

        public override bool OwnsInteractKey => _panel.Up;

        int IClockedShift.ClockLeft => Mathf.Max(0, _boxes - _stocked);

        float IClockedShift.ClockPay => UnitPay();

        public override bool QuitIsFailure => _stocked > 0 && _stocked * 2 < _boxes;

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
            _boxes = ShiftSize();
            if (_boxes <= 0)
            {
                Refusal = "reallife_job_no_daily";
                return false;
            }
            _left = _boxes;
            _carry = new JobCarry(Ctx.Logger);
            if (!JobSites.RandomClient(Ctx, Def, _boxes, ClientMin, ClientMax, Seconds, SecondsPerMetre, JobSites.ShopClient, out _client) || !StandPile())
            {
                Refusal = "reallife_job_no_client";
                return false;
            }
            _clientName = JobSites.NameOf(_client);
            _pileLabel = JobText.T("reallife_job_stocker_marker_pile");
            _doorLabel = JobText.T("reallife_job_stocker_marker_door");
            _shelfLabel = JobText.T("reallife_job_stocker_marker_shelf");
            _panelPile = JobText.T("reallife_job_stocker_panel_pile", "place", _clientName);
            _panelHands = JobText.T("reallife_job_obj_hands");
            _panelDoor = JobText.T("reallife_job_stocker_panel_door", "place", _clientName);
            _panelShelf = JobText.T("reallife_job_stocker_panel_shelf");
            _panelLeft = JobText.T("reallife_job_stocker_panel_left");
            _objClock = JobText.T("reallife_job_stocker_obj_clock");
            _endsAt = now + Seconds + JobSpots.Flat(Ctx.PlayerPosition, _client.Front) * SecondsPerMetre;
            BeginSpeed(now, _endsAt - now);
            _step = Step.ToPile;
            _figures = new ShiftFigures(Ctx, Def, "reallife_job_stocker_card_done");
            _clock = new ShiftClock(Ctx, this, this, "stocker", "reallife_job_stocker_unit", "reallife_job_stocker_units", "reallife_job_stocker_slip", canWork: ClientOpen,
                openFor: ClientOpenFor);
            Ctx.Logger?.Info("Jobs: stocking shelves at " + _clientName + " (" + _client.Name + "), " + _boxes + " boxes.");
            if (Meta.Dev.Verbose)
            {
                Ctx.Logger?.Info("Jobs: stocker: the pile is " + JobSpots.Flat(Ctx.PlayerPosition, _pileAt).ToString("F0") + " m from the player; shown in the game's own interface: job panel "
                                 + (_figures.Shown ? "copied" : "NOT copied (the panels over the pile, the door and the shelf are all there is)") + ", " + ShiftRates.Minutes(Def)
                                 + " game minutes a box by the clock at " + ShiftRates.Percent + "%.");
            }
            return true;
        }

        // The delivery stands a little to one side of the door, on pavement with room, out of the doorway.
        private bool StandPile()
        {
            Vector3 front = _client.Front;
            if (!CitySpots.Along(front, out Vector3 along))
            {
                return false;
            }
            for (int side = -1; side <= 1; side += 2)
            {
                if (!CitySpots.Stand(front + along * (side * 2.3f), 0.75f, out Vector3 at)
                    || !CitySpots.OnPavement(at + along * 0.6f, out _) || !CitySpots.OnPavement(at - along * 0.6f, out _))
                {
                    continue;
                }
                _pileAt = at;
                Quaternion turn = Quaternion.LookRotation(Vector3.Cross(along, Vector3.up));
                for (int i = 0; i < _boxes; i++)
                {
                    _pile[i] = Ctx.Props.Take(BoxProp);
                    if (_pile[i] == null)
                    {
                        return false;   // End gives back whatever was already taken
                    }
                    _pile[i].SetPositionAndRotation(at + turn * PilePlaces[i], turn * Quaternion.Euler(0f, (i * 37) % 11 - 5f, 0f));
                }
                _left = _boxes;
                return true;
            }
            return false;
        }

        public override void Tick(float now)
        {
            if (!ClientOpen())
            {
                return;
            }
            if (_clock.Hold())
            {
                Figures(now);   // the clock is working, or its offer is up with the game paused
                return;
            }
            if (now >= _endsAt && _step != Step.Stocking)
            {
                Ctx.Say("reallife_job_stocker_time", 8f);
                _panel.Hide();
                Conclude();
                return;
            }
            if (_step == Step.ToPile && _left <= 0 && _stocked >= _boxes)
            {
                Ctx.Say("reallife_job_stocker_done", 7f);   // the last of them was done by the clock
                _panel.Hide();
                Conclude();
                return;
            }
            bool inside = JobSites.Inside(_client);
            bool onFoot = !Ctx.Driving && !PlayerHelper.IsUsingVehicle;
            _wanted = false;
            if (_step != Step.ToPile && _step != Step.Stocking && _carry.Lost)
            {
                // The box left their hands some other way (the game stows one in a car they get into): it is back on the pile.
                _carry.ClearStowed();
                PutBack();
                _step = Step.ToPile;
                _hint = -1;
            }
            switch (_step)
            {
                case Step.ToPile:
                    if (inside)
                    {
                        Unmark();   // the way out is the shop's own door; the pile is marked once they are through it
                    }
                    else
                    {
                        Mark(_pileAt, _pileLabel);
                    }
                    bool atPile = !inside && onFoot && JobSpots.Flat(Ctx.PlayerPosition, _pileAt) < PileReach;
                    bool handsFull = PlayerHelper.IsHoldingItem;
                    Hint(inside ? 5 : atPile && handsFull ? 4 : 0);
                    if (atPile)
                    {
                        Up(handsFull ? _panelHands : _panelPile, ButtonPick, !handsFull, !handsFull, _pileAt + Vector3.up * PanelUp);
                    }
                    break;
                case Step.CarryIn:
                    if (inside)
                    {
                        _step = Step.ToShelf;
                        _hasShelf = false;
                        _tries = 0;
                        _nextTry = now + 0.6f;   // a moment for the shop to finish arriving round them
                        break;
                    }
                    Mark(_client.Front, _doorLabel);
                    Hint(1);
                    if (onFoot && JobSpots.Flat(Ctx.PlayerPosition, _client.Front) < DoorReach)
                    {
                        Up(_panelDoor, ButtonEnter, true, false, _client.Front + Vector3.up * PanelUp);
                    }
                    break;
                case Step.ToShelf:
                    ToShelf(now, inside);
                    break;
                case Step.Stocking:
                    if (now >= _stockEnds)
                    {
                        Stocked(now);
                    }
                    break;
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

        // The panel over a place: its words, the first button on the interact key, and Let time pass beside it.
        private void Up(string first, string action, bool actionOn, bool skip, Vector3 over)
        {
            _wanted = true;
            _panel.Offer(first, _panelLeft, JobText.Number(_boxes - _stocked), _panel.White, action, actionOn, skip ? ButtonSkip : null, true);
            _panel.Show(over, Ctx.Player);
        }

        private void Figures(float now)
        {
            bool clock = _clock.Working;
            if (clock)
            {
                Show(_objClock);
            }
            _figures.Objective(Objective);
            _figures.Units(_stocked + _clock.Done, _boxes);
            int seconds = clock ? int.MaxValue : Mathf.Max(0, Mathf.CeilToInt(_endsAt - now));
            if (seconds != _shownSeconds)
            {
                _shownSeconds = seconds;
                _figures.Limit(clock ? null : JobText.T("reallife_job_card_time_left", "time", ShiftFigures.Clock(seconds)), seconds <= 60 ? WorldPanel.Yellow() : _panel.White);
            }
            _figures.Money(Mathf.RoundToInt(_hand * UnitPay()) + _clockPay, 0);
            _figures.Time(Mathf.Max(0, _boxes - _stocked - _clock.Done) * _clock.MinutesEach);
        }

        private void ToShelf(float now, bool inside)
        {
            if (!inside)
            {
                _step = Step.CarryIn;   // stepped back out with the box
                _hint = -1;
                return;
            }
            if (!_hasShelf)
            {
                Unmark();
                if (now < _nextTry)
                {
                    return;
                }
                _nextTry = now + 1f;
                if (!JobSites.ShelfSpot(Ctx.Player.transform.position, _shelves, Mathf.Min(_hand, MaxBoxes), _tries < 2 ? ShelvesApart : 0f, out _shelfStand, out _shelfAt))
                {
                    if (++_tries >= 5)
                    {
                        Ctx.Say("reallife_job_stocker_no_shelf", 8f);   // no shelf to walk to: the round is called off, paid for what was done
                        Conclude();
                    }
                    return;
                }
                _hasShelf = true;
                Hint(2);
            }
            Mark(_shelfStand, _shelfLabel);
            if (JobSpots.Flat(Ctx.Player.transform.position, _shelfStand) < ShelfReach)
            {
                Up(_panelShelf, ButtonStock, true, false, new Vector3(_shelfAt.x, _shelfStand.y + PanelUp, _shelfAt.z));
            }
        }

        private void Act(string action)
        {
            if (Over)
            {
                return;
            }
            float now = Time.time;
            if (ReferenceEquals(action, ButtonEnter))
            {
                if (!ClientClosed(_client) && !JobSites.Enter(_client))
                {
                    ClientClosed(_client, true);
                }
            }
            else if (ReferenceEquals(action, ButtonPick) && _step == Step.ToPile && _left > 0 && !PlayerHelper.IsHoldingItem)
            {
                if (!_carry.Give())
                {
                    if (!PlayerHelper.IsHoldingItem)
                    {
                        Ctx.Say("reallife_job_stocker_no_box", 8f);
                        Conclude();
                    }
                    return;
                }
                _left--;
                Ctx.Props.Return(_pile[_left]);   // the top one
                _pile[_left] = null;
                _step = Step.CarryIn;
                _hint = -1;
            }
            else if (ReferenceEquals(action, ButtonStock) && _step == Step.ToShelf && _hasShelf)
            {
                _carry.Take();   // out of the arms: its contents go on the shelf, the box beside it
                Ctx.TurnPlayer(_shelfAt);
                float reach = Mathf.Max(1.2f, Ctx.Animations.Play(Ctx.Player.Character, JobClip.Stock));
                Ctx.Self.Hold(reach);
                _stockEnds = now + reach;
                _step = Step.Stocking;
                Unmark();
                Hint(3);
                // The emptied box goes on the floor beside them, where it is clear (never pushed into the shelf).
                Vector3 facing = Flat(_shelfAt - _shelfStand);
                Vector3 beside = _shelfStand + Vector3.Cross(Vector3.up, facing) * 0.6f;
                if (_hand < MaxBoxes && ShopFloor.Standable(beside, 0.15f, out Vector3 floor)
                    && (!NavMesh.FindClosestEdge(floor, out NavMeshHit edge, NavMeshHelper.NpcNavMeshFilter) || edge.distance > 0.3f))
                {
                    Transform box = Ctx.Props.Take(BoxProp);
                    if (box != null)
                    {
                        box.SetPositionAndRotation(floor, Quaternion.LookRotation(facing));
                        _shelved[_hand] = box;
                    }
                }
            }
        }

        private void Stocked(float now)
        {
            if (_hand < MaxBoxes)
            {
                _shelves[_hand] = _shelfStand;
            }
            _stocked++;
            _hand++;
            UnitDone(_hand, UnitPay());   // the shop's big tipper, if the offer drew one
            _shelvedAt = now;
            _hasShelf = false;
            _hint = -1;
            if (Meta.Dev.Verbose)
            {
                Ctx.Logger?.Info("Jobs: stocker: box shelved by hand; shelved " + _stocked + " of " + _boxes + ", " + _left + " still on the pile, "
                                 + Mathf.Max(0, Mathf.CeilToInt(_endsAt - now)) + " s of the delivery's time left.");
            }
            if (_left <= 0)
            {
                Ctx.Say("reallife_job_stocker_done", 7f);
                Conclude();
                return;
            }
            _step = Step.ToPile;
        }

        private void PutBack()
        {
            if (_left < _boxes && _pile[_left] == null)
            {
                Transform box = Ctx.Props.Take(BoxProp);
                if (box != null && CitySpots.Along(_pileAt, out Vector3 along))
                {
                    Quaternion turn = Quaternion.LookRotation(Vector3.Cross(along, Vector3.up));
                    box.SetPositionAndRotation(_pileAt + turn * PilePlaces[_left], turn);
                    _pile[_left] = box;
                }
                else if (box != null)
                {
                    Ctx.Props.Return(box);
                }
                _left++;
            }
        }

        private static Vector3 Flat(Vector3 direction)
        {
            direction.y = 0f;
            return direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.forward;
        }

        private void Hint(int which)
        {
            if (which == _hint)
            {
                return;
            }
            _hint = which;
            switch (which)
            {
                case 0: Show(JobText.T("reallife_job_stocker_obj_pile", "place", _clientName, "left", _left.ToString())); break;
                case 1: Show(JobText.T("reallife_job_stocker_obj_in", "place", _clientName)); break;
                case 2: Show(JobText.T("reallife_job_stocker_obj_shelf", "done", _stocked.ToString(), "count", _boxes.ToString())); break;
                case 3: Show(JobText.T("reallife_job_stocker_obj_stocking")); break;
                case 5: Show(JobText.T("reallife_job_stocker_obj_out", "left", _left.ToString())); break;
                default: Show(JobText.T("reallife_job_obj_hands")); break;
            }
        }

        private void Conclude()
        {
            bool all = _left <= 0 && _stocked >= _boxes;
            if (all && _hand == _stocked)
            {
                EndSpeed(Time.time);   // the whole delivery by hand
            }
            Settle(all);
            Finish(_stocked * 2 >= _boxes ? JobEnd.Done : JobEnd.Failed);
        }

        private void Settle(bool all)
        {
            if (!Owed(all, out PaySlip slip))
            {
                return;
            }
            _paid = true;
            Ctx.Pay(this, slip, "reallife_job_stocker_slip");
        }

        // A delivery that stops early is not the whole delivery.
        protected override bool Owed(out PaySlip slip) => Owed(false, out slip);

        private bool Owed(bool all, out PaySlip slip)
        {
            slip = default;
            if (_paid || _hand <= 0)
            {
                return false;
            }
            int each = UnitPay();
            slip = new PaySlip
            {
                Units = _hand,
                Base = _hand * each,
                Bonus = all ? Mathf.RoundToInt(_hand * each * 0.1f) : 0   // the clock's boxes earn no bonus
            };
            return true;
        }

        protected override ShiftClock Clocked => _clock;

        private bool ClientOpen() => !ClientClosed(_client);

        private int ClientOpenFor() => JobSites.MinutesOpen(_client);

        private void OfferSkip()
        {
            if (Over || _step != Step.ToPile || PlayerHelper.IsHoldingItem || Time.time - _shelvedAt < Debounce || !_clock.Offer())
            {
                return;
            }
            _panel.Hide();   // the game's activity panel has the keys while it is up
        }

        void IClockedShift.ClockStarted()
        {
            _panel.Hide();
        }

        void IClockedShift.ClockEnded(int count, int pay, float away)
        {
            // The boxes the clock shelved go from the pile, top first.
            for (int i = 0; i < count && _left > 0; i++)
            {
                _left--;
                Ctx.Props.Return(_pile[_left]);
                _pile[_left] = null;
            }
            _stocked += count;
            _clockPay += pay;
            _endsAt += away;   // the minutes the clock took are not the delivery's own time
            _shelvedAt = Time.time;
            _hint = -1;
            _shownSeconds = int.MinValue;
        }

        public override void End(JobEnd why)
        {
            _panel.Hide();
            _clock?.Close();   // the game's activity panel goes; nothing more is settled through it
            if (_carry != null)
            {
                _carry.Take();
                if (_stocked > 0 || _left < _boxes)
                {
                    _carry.ClearStowed();   // one the game stowed in a car on the way
                }
            }
            for (int i = 0; i < MaxBoxes; i++)
            {
                Ctx.Props.Return(_pile[i]);
                Ctx.Props.Return(_shelved[i]);
                _pile[i] = null;
                _shelved[i] = null;
            }
            if (why != JobEnd.Fault)
            {
                Settle(false);
            }
        }
    }
}
