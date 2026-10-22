using Helpers;
using UnityEngine;
using Random = UnityEngine.Random;

namespace RealLifeOverhaul.Events
{
    /// <summary>A street event's setting up: what stands on the closed road, and the people who walk in to it.</summary>
    internal sealed partial class StreetEventScene
    {
        // What stands on the closed road from the start, and the staff, who walk in.
        private bool Dress(float now)
        {
            switch (_kind)
            {
                case EventKind.Market:
                    AddStall("vendor_cart", -7.5f, CityLook.Vendor, true);
                    AddStall("fruit_stand", 0f, CityLook.Vendor, false);
                    AddStall("coffee_cart", 7.5f, CityLook.Vendor, false);   // never the newspaper kiosk: nobody shows inside it
                    AddMarshal(false);
                    return _stallCount > 0;
                case EventKind.FarmersMarket:
                    AddStall("fruit_stand", -7.5f, CityLook.Gardener, false);
                    AddStall("coffee_cart", 0f, CityLook.Vendor, false);
                    AddStall("fruit_stand", 7.5f, CityLook.Gardener, false);
                    AddMarshal(false);
                    return _stallCount > 0;
                case EventKind.Parade:
                    AddMarshal(false);
                    AddMarshal(true);
                    for (int i = 0; i < 4; i++)
                    {
                        if (Spot(-_half + 4f + (i / 2) * 1.6f, (i % 2 == 0 ? -0.8f : 0.8f), 0.4f, out Vector3 post))
                        {
                            Walk(Job.Marcher, CityLook.Civilian, post, post + _along * 5f, i, true);
                        }
                    }
                    return Has(Job.Marcher) >= 2;
                case EventKind.FunRun:
                    AddMarshal(false);
                    AddMarshal(true);
                    return true;
                case EventKind.Protest:
                    for (int i = 0; i < 5; i++)
                    {
                        if (Spot(-1.2f + (i / 3) * 1.3f, -1.3f + (i % 3) * 1.3f, 0.4f, out Vector3 post))
                        {
                            Walk(Job.Protester, CityLook.Civilian, post, post + _along * 6f, i, true);
                        }
                    }
                    if (Spot(-4f, -Mathf.Max(0.5f, _width - 1.1f), 0.45f, out Vector3 desk))
                    {
                        Walk(Job.Organiser, CityLook.Canvasser, desk, desk + _right * 4f, 0, true);
                    }
                    Officers(now);
                    return Has(Job.Protester) >= 3;
                case EventKind.FilmShoot:
                    return FilmSet();
                default:
                    _sky = _centre + _right * (Random.value < 0.5f ? 85f : -85f) + _along * Random.Range(-20f, 20f) + Vector3.up * 58f;
                    if (PlaceProp(EventProps.MortarRack, _half - 2.5f, 0f, 0.7f, 0.4f, 0f) == null && !_director.Sky.Ready())
                    {
                        return false;
                    }
                    AddMarshal(false);
                    return _director.Sky.Ready();
            }
        }

        private bool FilmSet()
        {
            Transform camera = PlaceProp(EventProps.FilmCamera, -3.5f, 0f, 0.5f, 0.5f, 0f);
            if (camera == null)
            {
                return false;
            }
            PlaceProp(EventProps.LightStand, -1.5f, -Mathf.Min(2.2f, _width - 0.8f), 0.5f, 0.5f, 25f);
            PlaceProp(EventProps.LightStand, -1.5f, Mathf.Min(2.2f, _width - 0.8f), 0.5f, 0.5f, -25f);
            PlaceProp(EventProps.Clapper, -5.6f, Mathf.Min(1.4f, _width - 0.8f), 0.4f, 0.4f, 0f);
            Vector3 lens = _centre - _along * 3.5f;
            // camera_operate puts the right eye at film_camera's eyecup only from OperatorBehind on the lens's line; a hand's
            // breadth off misses it, so the place is taken from the camera as it stands, walked to exactly (Arrivals), and the
            // clip played only there.
            _operatorPost = camera.position - camera.forward * OperatorBehind;
            _operatorAtCamera = false;
            if (Spot(-3.5f - OperatorBehind, 0f, 0.35f, out Vector3 behind))
            {
                if (CitySpots.Flat(behind - _operatorPost) < OperatorTolerance * OperatorTolerance)
                {
                    behind = new Vector3(_operatorPost.x, behind.y, _operatorPost.z);
                }
                Walk(Job.Operator, CityLook.Civilian, behind, camera.position + camera.forward * 5f, 0, true);
            }
            if (Spot(-5.6f, Mathf.Min(1.4f, _width - 0.8f) - 0.9f, 0.4f, out Vector3 chair))
            {
                Walk(Job.FilmDirector, CityLook.Civilian, chair, lens + _along * 5f, 0, true);
            }
            if (Spot(-6.5f, -Mathf.Min(1.6f, _width - 0.8f), 0.4f, out Vector3 radio))
            {
                Walk(Job.Assistant, CityLook.Courier, radio, _centre, 0, true);
            }
            if (Spot(1.6f, -0.55f, 0.35f, out Vector3 first) && Spot(1.6f, 0.55f, 0.35f, out Vector3 second))
            {
                Walk(Job.Actor, CityLook.Civilian, first, second, 0, true);
                Walk(Job.Actor, CityLook.Civilian, second, first, 1, true);
            }
            AddMarshal(false);
            return Has(Job.Actor) == 2 && Has(Job.Operator) == 1;
        }

        // A stall against one side of the road, its counter to the middle; its vendor walks in.
        private void AddStall(string prop, float along, CityLook look, bool parasol)
        {
            if (_stallCount >= MaxStalls)
            {
                return;
            }
            Vector3 size = PropLibrary.SizeOf(prop);
            if (size == Vector3.zero)
            {
                return;   // no such stall in the bundle: there is no stand-in for one
            }
            float depth = size.z * 0.5f;
            float standsBack = depth + (depth > 0.5f ? 0.3f : 0.45f);   // the vendor, behind the stall: nearer a deep one, whose counter is at its back
            float across = -(_width - standsBack - depth - 0.55f);      // so that the vendor has half a metre of road behind them
            if (across > -0.2f)
            {
                return;   // the road is too narrow for a stall, its vendor and its customers
            }
            for (int attempt = 0; attempt < 4; attempt++)
            {
                float at = along + (attempt == 0 ? 0f : attempt == 1 ? 2f : attempt == 2 ? -2f : 3.5f);
                Vector3 place = Point(at, across);
                Quaternion facing = Quaternion.LookRotation(_right);   // customers at +Z: towards the middle of the road
                if (!SceneParking.IsFree(place, facing, size.x * 0.5f, depth, 0.25f) || !StreetSpots.OnRoad(place, 0.6f, out _))
                {
                    continue;
                }
                Vector3 customer = place + _right * (depth + 0.65f);
                Vector3 behind = place - _right * standsBack;
                if (!StreetSpots.OnRoad(customer, 0.5f, out customer) || !StreetSpots.OnRoad(behind, 0.4f, out behind) || !CitySpots.ClearQuietly(behind, 0.3f))
                {
                    continue;
                }
                Transform stall = _director.Props.Stand(prop, place, facing.eulerAngles.y);
                if (stall == null)
                {
                    return;
                }
                _dressing.Add(stall);
                if (parasol)
                {
                    Vector3 pole = place - _along * (size.x * 0.5f + 0.7f) - _right * 0.2f;
                    if (SceneParking.IsFree(pole, facing, 0.25f, 0.25f, 0.1f))
                    {
                        Transform shade = _director.Props.Stand("vendor_umbrella", pole, 0f);
                        if (shade != null)
                        {
                            _dressing.Add(shade);
                        }
                    }
                }
                int slot = _stallCount++;
                _stalls[slot] = new Stall { Customer = customer, Counter = place, Behind = behind };
                Walk(Job.Vendor, look, behind, customer, slot, true);
                return;
            }
        }

        private void AddMarshal(bool farEnd)
        {
            Vector3 post = Point((farEnd ? 1f : -1f) * (_half + 2.2f), -Mathf.Max(0f, _width - 1.2f));
            if (StreetSpots.OnRoad(post, 1.5f, out post) && CitySpots.ClearQuietly(post, 0.4f))
            {
                Walk(Job.Marshal, CityLook.RoadWorker, post, post + _along * (farEnd ? 12f : -12f), farEnd ? 1 : 0, true);
            }
        }

        // Two officers at the far end, facing the crowd: only there to watch.
        private void Officers(float now)
        {
            if (!Settings.Police || !Settings.People)
            {
                return;
            }
            for (int i = 0; i < 2; i++)
            {
                Vector3 post = Point(_half - 2.5f, i == 0 ? -1.2f : 1.2f);
                ActorRole officer = _director.Officers[i];
                if (StreetSpots.OnRoad(post, 1f, out post) && officer.Enter(post, ActorLook.Officer, 1.5f, true))
                {
                    officer.Go(post, Pace.Walk, _centre, 10f);
                    officer.Work("guard_stand");
                    _officers++;
                }
            }
        }

        private Vector3 Point(float along, float across)
        {
            Vector3 point = _centre + _along * along + _right * across;
            if (StreetSpots.Ground(point, out float ground))
            {
                point.y = ground;
            }
            return point;
        }

        // A place on the closed carriageway with room for a person.
        private bool Spot(float along, float across, float room, out Vector3 spot)
        {
            across = Mathf.Clamp(across, -(_width - 0.6f), _width - 0.6f);
            return StreetSpots.OnRoad(Point(along, across), 0.5f, out spot) && CitySpots.ClearQuietly(spot, room);
        }

        private Transform PlaceProp(string id, float along, float across, float halfWidth, float halfLength, float turn)
        {
            Vector3 place = Point(along, Mathf.Clamp(across, -(_width - halfWidth), _width - halfWidth));
            Quaternion facing = Quaternion.LookRotation(_along) * Quaternion.Euler(0f, turn, 0f);
            if (!SceneParking.IsFree(place, facing, halfWidth, halfLength, 0.2f))
            {
                return null;
            }
            Transform prop = _director.Props.Stand(id, place, facing.eulerAngles.y);
            if (prop != null)
            {
                _dressing.Add(prop);
            }
            return prop;
        }

        private int Has(Job job)
        {
            int has = 0;
            for (int i = 0; i < _count; i++)
            {
                has += _cast[i].Job == job ? 1 : 0;
            }
            return has;
        }

        // One more person, walking in from out of view (a test may not wait for that: they are put at their place).
        private bool Walk(Job job, CityLook look, Vector3 post, Vector3 face, int slot, bool road, bool jog = false)
        {
            if (_count >= MaxCast)
            {
                return false;
            }
            EventPeople people = _director.People;
            CityPerson who = people.WalkIn(look, post, road, jog);
            bool walking = who != null;
            if (who == null && _test)
            {
                who = people.Put(look, post, face, road);
            }
            if (who == null)
            {
                return false;
            }
            _cast[_count++] = new Member { Who = who, Job = job, Step = walking ? 0 : 1, Post = post, Face = face, Slot = slot, Until = Time.time + 70f };
            if (job == Job.Runner)
            {
                people.Wear(who, EventProps.Bib, HumanBodyBones.UpperChest, 0f, 0f);
            }
            else if (job == Job.Marcher || job == Job.Protester)
            {
                if (people.CarryStaff(who, job == Job.Marcher && slot < 2 ? EventProps.Banner : EventProps.Placard))
                {
                    who.WorkOver(EventPeople.StaffClip, people.Props.Shelf, 0.2f, ClipEnd.Return, BodyMask.RightArm, false);
                }
            }
            if (!walking)
            {
                Arrive(ref _cast[_count - 1], Time.time);
            }
            return true;
        }

        private void Drop(int index, bool sendOff)
        {
            Member member = _cast[index];
            if (member.Job == Job.Browser && member.Slot >= 0)
            {
                _stalls[member.Slot].Taken = false;
            }
            if (sendOff && !member.Who.Lost)
            {
                _director.People.SendOff(member.Who, member.Job == Job.Runner);
            }
            else if (!member.Who.Lost)
            {
                _director.People.Release(member.Who);
            }
            _cast[index] = _cast[--_count];
            _cast[_count] = default;
        }

        // Somebody has reached their place: they stand, turn to what they are there for and take up what they do.
        private void Arrive(ref Member member, float now)
        {
            CityPerson who = member.Who;
            CityProps shelf = _director.People.Props.Shelf;
            member.Step = 1;
            if (member.Job == Job.Runner || (member.Job == Job.Marcher && _phase == Phase.Running))
            {
                return;   // they keep moving: see Second
            }
            who.Stand();
            who.Face(member.Face);
            switch (member.Job)
            {
                case Job.Marshal:
                    if (!who.Work("paddle_hold", shelf, 0.3f))
                    {
                        who.Work("guard_stand", shelf);
                    }
                    break;
                case Job.Vendor:
                    who.Work("counter_wipe", shelf, 0.3f, ClipEnd.Return, true, Random.value * 2f);
                    break;
                case Job.Browser:
                    who.Work("queue_wait", shelf, 0.3f, ClipEnd.Return, true, Random.value * 4f);
                    member.Until = now + Random.Range(4f, 8f);
                    break;
                case Job.Onlooker:
                case Job.Watcher:
                    who.Work("watch_nod", shelf, 0.3f, ClipEnd.Return, true, Random.value * 3f);
                    member.Until = now + Random.Range(3f, 9f);
                    break;
                case Job.Organiser:
                    who.Work("clipboard_write", shelf);
                    break;
                case Job.Operator:
                    // At the viewfinder only if they really stand where the clip was made for; anywhere else they stand by it.
                    _operatorAtCamera = who.Near(_operatorPost, OperatorTolerance) && ClipLibrary.Has("camera_operate");
                    who.Work(_operatorAtCamera ? "camera_operate" : "guard_stand", shelf);
                    member.Until = now + 4f;
                    break;
                case Job.FilmDirector:
                    who.Work("clipboard_write", shelf);
                    break;
                case Job.Assistant:
                    who.Work("radio_talk", shelf);
                    break;
                case Job.Actor:
                    who.Work("arms_crossed", shelf, 0.3f, ClipEnd.Return, true, member.Slot * 3f);
                    break;
                case Job.Protester:
                    who.Work(EventPeople.StaffClip, shelf, 0.3f, ClipEnd.Return, false, member.Slot * 0.7f);   // the staff already in hand
                    break;
            }
        }
    }
}
