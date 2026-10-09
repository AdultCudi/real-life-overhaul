using Extensions;
using Helpers;
using UnityEngine;
using UnityEngine.AI;

namespace RealLifeOverhaul
{
    /// <summary>
    /// Shelf stocker. A delivery for a shop near the player has been set down on the pavement
    /// by its door: a pile of boxes. Pick one up (the game's own two-handed carry), take it in
    /// through the shop's door, carry it to the shelf it is for and stock the shelf; then back
    /// out for the next. Paid per box shelved, a little more for clearing the pile in time.
    ///
    /// The shop is one of the city's own that is not the player's, has shelves and is open; the
    /// shelf is one of its real shelves, and the place to stand at it is on the shop's own
    /// navigation mesh. Stocking is JobClip.Stock on the player (the mod's stock_shelf clip, or
    /// the game's reach), who is held still for it; the emptied box stays on the floor by the
    /// shelf until the shift ends. Nothing is added to the shop's stock: the boxes are the job's.
    /// </summary>
    public sealed class StockerJob : Job
    {
        private enum Step { ToPile, CarryIn, ToShelf, Stocking }

        private const int MaxBoxes = 6;
        private const float ClientMin = 15f;
        private const float ClientMax = 260f;
        private const float DoorReach = 3.2f;
        private const float PileReach = 2.2f;
        private const float ShelfReach = 1.3f;
        private const float ShelvesApart = 1.8f;
        private const float Seconds = 300f;
        private const float SecondsPerMetre = 0.5f;
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
        private int _stocked;
        private float _endsAt;
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
        private string _promptPick;
        private string _promptEnter;
        private string _promptStock;

        public override JobDef Def => _def;

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
            _boxes = Ctx.FullShift ? MaxBoxes : Mathf.Min(MaxBoxes, JobState.LeftToday(Def));
            if (_boxes <= 0)
            {
                Refusal = "reallife_job_no_daily";
                return false;
            }
            _left = _boxes;
            _carry = new JobCarry(Ctx.Logger);
            if (!JobSpots.RandomDoor(Ctx.PlayerPosition, ClientMin, ClientMax, null, 0f, out _client, JobSites.ShopClient, 96) || !StandPile())
            {
                Refusal = "reallife_job_no_client";
                return false;
            }
            _clientName = JobSites.NameOf(_client);
            _pileLabel = JobText.T("reallife_job_stocker_marker_pile");
            _doorLabel = JobText.T("reallife_job_stocker_marker_door");
            _shelfLabel = JobText.T("reallife_job_stocker_marker_shelf");
            _promptPick = JobText.T("reallife_job_stocker_prompt_pick");
            _promptEnter = JobText.T("reallife_job_prompt_enter");
            _promptStock = JobText.T("reallife_job_stocker_prompt_stock");
            _endsAt = now + Seconds + JobSpots.Flat(Ctx.PlayerPosition, _client.Front) * SecondsPerMetre;
            _step = Step.ToPile;
            Ctx.Logger?.Info("Jobs: stocking shelves at " + _clientName + " (" + _client.Name + "), " + _boxes + " boxes.");
            return true;
        }

        // The delivery on the pavement: a little to one side of the door, on pavement with room, out of the doorway.
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
            ClockSeconds = Mathf.CeilToInt(_endsAt - now);
            if (now >= _endsAt && _step != Step.Stocking)
            {
                Ctx.Say("reallife_job_stocker_time", 8f);
                Conclude();
                return;
            }
            bool inside = JobSites.Inside(_client);
            bool onFoot = !Ctx.Driving && !PlayerHelper.IsUsingVehicle;
            if (_step != Step.ToPile && _step != Step.Stocking && _carry.Lost)
            {
                // The box left their hands some other way (the game stows one if they get into a car): it is back on the pile.
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
                    Prompt = atPile && !handsFull ? _promptPick : null;
                    break;
                case Step.CarryIn:
                    if (inside)
                    {
                        _step = Step.ToShelf;
                        _hasShelf = false;
                        _tries = 0;
                        _nextTry = now + 0.6f;   // a moment for the shop to finish arriving round them
                        Prompt = null;
                        break;
                    }
                    Mark(_client.Front, _doorLabel);
                    Hint(1);
                    Prompt = onFoot && JobSpots.Flat(Ctx.PlayerPosition, _client.Front) < DoorReach ? _promptEnter : null;
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
                Prompt = null;
                Unmark();
                if (now < _nextTry)
                {
                    return;
                }
                _nextTry = now + 1f;
                if (!JobSites.ShelfSpot(Ctx.Player.transform.position, _shelves, Mathf.Min(_stocked, MaxBoxes), _tries < 2 ? ShelvesApart : 0f, out _shelfStand, out _shelfAt))
                {
                    if (++_tries >= 5)
                    {
                        Ctx.Say("reallife_job_stocker_no_shelf", 8f);   // a shop with no shelf to walk to: the round is called off, paid for what was done
                        Conclude();
                    }
                    return;
                }
                _hasShelf = true;
                Hint(2);
            }
            Mark(_shelfStand, _shelfLabel);
            Prompt = JobSpots.Flat(Ctx.Player.transform.position, _shelfStand) < ShelfReach ? _promptStock : null;
        }

        public override void Interact(float now)
        {
            if (Prompt == null)
            {
                return;
            }
            string prompt = Prompt;
            Prompt = null;
            if (ReferenceEquals(prompt, _promptEnter))
            {
                if (_client.Building != null)
                {
                    _client.Building.Interact();
                }
            }
            else if (ReferenceEquals(prompt, _promptPick) && _left > 0)
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
            else if (ReferenceEquals(prompt, _promptStock) && _step == Step.ToShelf && _hasShelf)
            {
                _carry.Take();   // out of the arms: its contents go on the shelf, the box by it
                Ctx.TurnPlayer(_shelfAt);
                float reach = Mathf.Max(1.2f, Ctx.Animations.Play(Ctx.Player.Character, JobClip.Stock));
                Ctx.Self.Hold(reach);
                _stockEnds = now + reach;
                _step = Step.Stocking;
                Unmark();
                Hint(3);
                // The emptied box on the floor beside them, where there is clear floor for it (never pushed into the shelf).
                Vector3 facing = Flat(_shelfAt - _shelfStand);
                Vector3 beside = _shelfStand + Vector3.Cross(Vector3.up, facing) * 0.6f;
                if (_stocked < MaxBoxes && ShopFloor.Standable(beside, 0.15f, out Vector3 floor)
                    && (!NavMesh.FindClosestEdge(floor, out NavMeshHit edge, NavMeshHelper.NpcNavMeshFilter) || edge.distance > 0.3f))
                {
                    Transform box = Ctx.Props.Take(BoxProp);
                    if (box != null)
                    {
                        box.SetPositionAndRotation(floor, Quaternion.LookRotation(facing));
                        _shelved[_stocked] = box;
                    }
                }
            }
        }

        private void Stocked(float now)
        {
            if (_stocked < MaxBoxes)
            {
                _shelves[_stocked] = _shelfStand;
            }
            _stocked++;
            _hasShelf = false;
            _hint = -1;
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
            Settle(_left <= 0 && _stocked >= _boxes);
            Finish(_stocked * 2 >= _boxes ? JobEnd.Done : JobEnd.Failed);
        }

        private void Settle(bool all)
        {
            if (_paid || _stocked <= 0 || Ctx == null)
            {
                return;
            }
            _paid = true;
            int each = UnitPay();
            var slip = new PaySlip
            {
                Units = _stocked,
                Base = _stocked * each,
                Bonus = all ? Mathf.RoundToInt(_stocked * each * 0.1f) : 0
            };
            Ctx.Pay(this, slip, "reallife_job_stocker_slip");
        }

        public override void End(JobEnd why)
        {
            if (Ctx == null)
            {
                return;
            }
            Prompt = null;
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
