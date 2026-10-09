using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// A delivery. The courier WALKS IN from out of view behind a sack truck with a parcel on it
    /// (the game's own walk, the arms of the truck clip over it, the truck going with them),
    /// stands the truck up on the pavement, goes round to its front, lifts the parcel off, carries
    /// it to the shopfront, puts it down by the wall, walks back, takes the truck and walks away
    /// out of view. The parcel stays where it was left until the site goes.
    ///
    /// Every place is measured before anybody is put out: where the truck stands, where the
    /// courier stands behind it, beside it and in front of it, and the place at the wall. The
    /// numbers are the ones the clips were made with (CityAssets/scripts/clips_life.py).
    ///
    /// Who has what, at every moment: the truck is the courier's (brought along) or stands in
    /// the dressing; the parcel rides on the truck as its child, is in the courier's hands, or
    /// stands in the dressing. End parts parcel from truck first, and then everything goes back
    /// the usual way.
    /// </summary>
    public sealed class CourierActivity : CityActivity
    {
        private const float AxleAhead = 1.09f;      // the truck's axle ahead of whoever pushes it (hand_truck_push)
        private const float TruckTilt = 38f;        // degrees it leans back on to the hands
        private const float AxleUp = 0.11f;
        private const float PlateAhead = 0.14f;     // the middle of the toe plate ahead of the axle, where the parcel rides
        private const float BoxAhead = 0.40f;       // a box on the ground ahead of whoever picks it up or sets it down
        private const float WallGap = 0.64f;        // the courier's feet from the wall when the parcel goes down against it
        private const float Patience = 60f;         // seconds any one walk may take

        private const int Lifted = 1, Carrying = 2, Down = 3;

        private enum Step { Coming, Parking, ToSide, ToFront, Lifting, ToDoor, Setting, Back, Taking, Leaving, Gone }

        private Vector3 _truckAt, _behind, _side, _front, _door, _along, _wallOut;
        private Transform _truck, _parcel;
        private CityPerson _courier;
        private Step _step;
        private float _until;

        public override bool Find(CitySite site)
        {
            if (!CitySpots.Shopfront(site.Anchor, WallGap, 0.4f, out _door, out _wallOut) || !CitySpots.Along(_door, out _along))
            {
                return false;
            }
            // The truck is left out on the pavement a few steps from the wall, along the way the pavement runs; the
            // courier goes round it on whichever side has room, never through it.
            for (int way = 0; way < 2; way++)
            {
                Vector3 along = way == 0 ? _along : -_along;
                Vector3 across = Vector3.Cross(Vector3.up, along);
                if (!CitySpots.Stand(_door + _wallOut * 1.5f + along * 1.6f, 0.45f, out _truckAt) || !CitySpots.Stand(_truckAt - along * AxleAhead, 0.35f, out _behind)
                    || !CitySpots.Stand(_truckAt + along * (PlateAhead + BoxAhead), 0.35f, out _front) || !CitySpots.OnPavement(_door - _wallOut * BoxAhead * 0.6f, out _))
                {
                    continue;
                }
                if (CitySpots.Stand(_truckAt + across * 0.8f, 0.3f, out _side) || CitySpots.Stand(_truckAt - across * 0.8f, 0.3f, out _side))
                {
                    _along = along;
                    Centre = _door;
                    return true;
                }
            }
            return false;
        }

        public override bool Begin(CitySite site)
        {
            Start(site);
            if (!ClipLibrary.Has("hand_truck_push") || !ClipLibrary.Has("box_pick_up") || !ClipLibrary.Has("box_set_down") || !ClipLibrary.Has("carry_box_hold"))
            {
                site.Refusal = CityRefusal.NoProp;
                return false;
            }
            _courier = Arrive(site, CityLook.Courier, _behind);
            if (_courier == null)
            {
                return false;   // Arrive has said why
            }
            _truck = Props.Take("hand_truck");
            _parcel = Props.Take("parcel_m");
            if (_truck == null || _parcel == null || !Wheel())
            {
                Props.Return(_truck);
                Props.Return(_parcel);
                _truck = null;
                _parcel = null;
                site.Refusal = CityRefusal.NoProp;
                End();
                return false;
            }
            // The parcel rides on the toe plate.
            _parcel.SetParent(_truck, false);
            _parcel.localPosition = new Vector3(0f, 0.03f, PlateAhead);
            _parcel.localRotation = Quaternion.identity;
            _parcel.localScale = Vector3.one;
            Go(Step.Coming, Time.time);
            return true;
        }

        public override bool Tick(float now)
        {
            TickDepartures();
            if (_step == Step.Gone)
            {
                return !Over || CitySpots.InView(Centre);   // the parcel waits at the door until the site's time is up
            }
            if (_courier == null || (_step != Step.Leaving && _courier.Lost))
            {
                Over = true;
            }
            if (!Over && _step != Step.Leaving && _step != Step.Lifting && _step != Step.Setting && now >= _until)
            {
                // A walk that never got there (the way was blocked): they stop where they are and the site ends when nobody is looking.
                CityLifeDirector.Logger?.Info($"City life: the courier gave up at step {_step} (did not get there in {Patience:F0} s).");
                _courier.Stand();
                Over = true;
            }
            if (Over)
            {
                return CitySpots.InView(Centre) || (_courier != null && !_courier.Lost && CitySpots.InView(_courier.Position));
            }
            switch (_step)
            {
                case Step.Coming:
                    if (At(_behind, 0.7f))
                    {
                        _courier.Stand();
                        _courier.Face(_truckAt);
                        Go(Step.Parking, now);
                    }
                    break;
                case Step.Parking:
                    // The truck is stood up where it was measured to stand, and left.
                    _courier.StopOver(0.3f);
                    _courier.Leave("hand_truck");
                    _truck.SetPositionAndRotation(_truckAt, Quaternion.LookRotation(_along));
                    Dressing.Add(_truck);
                    _courier.WalkTo(_side);
                    Go(Step.ToSide, now);
                    break;
                case Step.ToSide:
                    if (At(_side, 0.6f))
                    {
                        _courier.WalkTo(_front);
                        Go(Step.ToFront, now);
                    }
                    break;
                case Step.ToFront:
                    if (At(_front, 0.45f))
                    {
                        _courier.Stand();
                        _courier.Face(_truckAt);
                        if (_courier.Work("box_pick_up", Props, 0.3f, ClipEnd.Hold, false) && ClipLibrary.TryGet("box_pick_up", out CityClip lift))
                        {
                            CityLifeDirector.After(lift.Mark("grip", 1f), this, Lifted, _courier);
                            CityLifeDirector.After(lift.Length, this, Carrying, _courier);
                            Go(Step.Lifting, now);
                        }
                    }
                    break;
                case Step.ToDoor:
                    if (At(_door, 0.45f))
                    {
                        _courier.Stand();
                        _courier.Face(_door - _wallOut);
                        _courier.StopOver(0.3f);
                        if (_courier.Work("box_set_down", Props, 0.3f) && ClipLibrary.TryGet("box_set_down", out CityClip down))
                        {
                            CityLifeDirector.After(down.Mark("release", 1.2f), this, Down, _courier);
                            Go(Step.Setting, now);
                            _until = now + down.Length + 1.2f;   // down, up, a look at it
                        }
                    }
                    break;
                case Step.Setting:
                    if (now >= _until)
                    {
                        _courier.WalkTo(_behind);
                        Go(Step.Back, now);
                    }
                    break;
                case Step.Back:
                    if (At(_behind, 0.6f))
                    {
                        _courier.Stand();
                        _courier.Face(_truckAt);
                        Go(Step.Taking, now);
                    }
                    break;
                case Step.Taking:
                    // Facing it now: the truck goes back on to the hands where it stands, and away.
                    Dressing.Remove(_truck);
                    if (!Wheel())
                    {
                        Dressing.Add(_truck);
                    }
                    Depart(_courier);
                    Go(Step.Leaving, now);
                    break;
                case Step.Leaving:
                    if (!Cast.Contains(_courier))
                    {
                        _truck = null;   // went back to the shelf with the courier
                        _courier = null;
                        _step = Step.Gone;
                    }
                    break;
            }
            return true;
        }

        public override void Moment(int what, CityPerson who, float now)
        {
            if (who != _courier || _courier == null || _courier.Lost || Over)
            {
                return;
            }
            switch (what)
            {
                case Lifted:
                    if (_parcel != null && _step == Step.Lifting)
                    {
                        _parcel.SetParent(null, true);
                        if (!_courier.Take(_parcel, "box_pick_up", Props))
                        {
                            // The hands would not take it: it stays on the ground by the truck, and the round ends.
                            Dressing.Add(_parcel);
                            _parcel = null;
                            Over = true;
                        }
                    }
                    break;
                case Carrying:
                    if (_step == Step.Lifting)
                    {
                        _courier.WorkOver("carry_box_hold", Props, 0.2f, ClipEnd.Return, BodyMask.Arms, false);
                        _courier.WalkTo(_door);
                        Go(Step.ToDoor, now);
                    }
                    break;
                case Down:
                    if (_step == Step.Setting)
                    {
                        // Out of the hands and on the ground by the wall, where the clip put it.
                        Transform box = _courier.Release(true);
                        if (box != null)
                        {
                            Vector3 at = box.position;
                            if (CitySpots.Ground(at, out float ground))
                            {
                                at.y = ground;
                            }
                            box.SetPositionAndRotation(at, Quaternion.Euler(0f, box.eulerAngles.y, 0f));
                            Dressing.Add(box);
                        }
                        _parcel = null;
                    }
                    break;
            }
        }

        public override void End()
        {
            // The parcel rides on the truck as its child until it is lifted: apart before either goes back to the shelf.
            if (_parcel != null && _truck != null && _parcel.parent == _truck)
            {
                _parcel.SetParent(null, true);
                Props?.Return(_parcel);
            }
            // The truck itself is always somebody's: the courier's (it goes back with them), the dressing's, or among
            // what fell when the courier was knocked down (which Disturbed made the dressing's).
            _parcel = null;
            _truck = null;
            _courier = null;
            _step = Step.Gone;
            base.End();
        }

        private void Go(Step step, float now)
        {
            _step = step;
            _until = now + Patience;
        }

        // There; or stopped short of there, and then on again.
        private bool At(Vector3 place, float within)
        {
            if (!_courier.Arrived)
            {
                return false;
            }
            if (_courier.Near(place, within))
            {
                return true;
            }
            _courier.WalkTo(place);
            return false;
        }

        // The truck in the courier's hands: leant back, its axle the clip's distance ahead, the arms of the pushing clip over the game's walk.
        private bool Wheel()
        {
            Quaternion lean = Quaternion.Euler(-TruckTilt, 0f, 0f);
            Vector3 axle = new Vector3(0f, AxleUp, AxleAhead);
            if (!_courier.Bring(_truck, null, axle - lean * new Vector3(0f, AxleUp, 0f), lean))
            {
                return false;
            }
            _courier.WorkOver("hand_truck_push", Props, 0.3f, ClipEnd.Return, BodyMask.Arms, false);
            return true;
        }
    }
}
