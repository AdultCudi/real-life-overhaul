using System.Collections.Generic;
using BigAmbitions.Characters;
using UnityEngine;
using Random = UnityEngine.Random;

namespace RealLifeOverhaul.Law
{
    // The people of a furnished station, a handful at most (StationCrew): the desk sergeant behind the counter; one or two
    // officers seated at the desks, typing as the game's own employees type at a desk computer (its UsingDesktopComputer,
    // played on the game's sitting pose every few seconds, as DesktopWorkstationController does); an officer on a break at
    // the break table; one walking the rooms (to the coffee machine, the briefing screen, the cell, a word with somebody at a
    // desk, the notice board); and now and then somebody of the public, who comes in, sits on the waiting chairs, goes up to
    // the side of the counter and leaves. The officers come on and go off shift through the door: whoever leaves is followed
    // a while later by somebody else coming in.
    //
    // Everybody is put out only while the player is in the room (Begin, under the fade, seats those already at work at
    // once); End takes them all away. They are looked after once a second, from the law's own tick, and nothing runs per
    // frame. All of them are the mod's pool's own people (People/ActorPool), most of them ones the pool may take back for a
    // scene at any moment: whoever goes is replaced later, through the door.
    internal sealed partial class StationInterior
    {
        private const float DeskStayMin = 140f, DeskStayMax = 280f;
        private const float BreakStayMin = 50f, BreakStayMax = 120f;
        private const float WaitStayMin = 25f, WaitStayMax = 70f;
        private const float ShiftGapMin = 35f, ShiftGapMax = 80f;
        private const float VisitorGapMin = 30f, VisitorGapMax = 100f;

        private enum Doing { Away, Coming, Seated, Errand, Leaving }

        private sealed class Member
        {
            public readonly ActorRole Role = new ActorRole();
            public SeatUse Use;
            public ActorLook Look;
            public bool Presence;
            public int Seat = -1;
            public Doing Doing;
            public float Until;
            public Vector3 ErrandAt, ErrandFace;
            public string ErrandClip;
            public bool ErrandStarted;
            public bool ThenLeave;
        }

        private struct Stop
        {
            public Vector3 At, Face;
            public string Clip;
        }

        private readonly List<Member> _members = new List<Member>(StationCrew.Most);
        private readonly List<Stop> _stops = new List<Stop>(10);
        private readonly ActorRole _walker = new ActorRole();
        private Doing _walking;
        private float _walkerUntil;
        private int _walkerStop = -1;
        private bool _hasWalker;
        private StationCrew _crew;

        // ---------------------------------------------------------------- coming in

        private void BeginCast(bool desk)
        {
            EndCast();
            if (!_cast)
            {
                return;
            }
            int deskSeats = 0, breakSeats = 0, waitingSeats = 0;
            for (int i = 0; i < _seats.Count; i++)
            {
                deskSeats += _seats[i].Use == SeatUse.Desk ? 1 : 0;
                breakSeats += _seats[i].Use == SeatUse.Break ? 1 : 0;
                waitingSeats += _seats[i].Use == SeatUse.Waiting ? 1 : 0;
            }
            _crew = StationCrew.For(deskSeats, breakSeats, waitingSeats > 0 || _hasVisitor ? Mathf.Max(1, waitingSeats) : 0, _length, true);
            for (int i = 0; i < _crew.AtDesks; i++)
            {
                _members.Add(new Member { Use = SeatUse.Desk, Look = ActorLook.Officer, Presence = true });
            }
            if (_crew.OnBreak > 0)
            {
                _members.Add(new Member { Use = SeatUse.Break, Look = ActorLook.Officer, Presence = false });
            }
            if (_crew.Visitors > 0)
            {
                _members.Add(new Member { Use = SeatUse.Waiting, Look = ActorLook.Civilian, Presence = true });
            }
            BuildStops();
            _hasWalker = _crew.Walking > 0 && _stops.Count >= 2;
            _walking = Doing.Away;
            _walkerUntil = 0f;
            float now = Time.time;
            if (!Settings.People)
            {
                return;
            }
            // Those already at work when the player comes in are there at once (the screen is still dark).
            PostSergeant();
            for (int i = 0; i < _members.Count; i++)
            {
                Member m = _members[i];
                bool there = m.Use != SeatUse.Waiting || Random.value < 0.5f;
                if (!there || !SitDownAtOnce(m, now))
                {
                    m.Doing = Doing.Away;
                    m.Until = now + (m.Use == SeatUse.Waiting ? Random.Range(10f, 40f) : Random.Range(8f, 25f));
                }
            }
            if (_hasWalker)
            {
                int stop = Random.Range(0, _stops.Count - 1);   // (the last stop is the door)
                if (Spawn(_walker, _stops[stop].At, ActorLook.Officer, true))
                {
                    ArriveAtStop(stop, now);
                }
                else
                {
                    _walkerUntil = now + 20f;
                }
            }
            _fitted.Append("cast: ").Append(_crew.Total).Append(" (").Append(_crew.AtDesks).Append(" at desks, ").Append(_crew.OnBreak).Append(" on a break, ")
                .Append(_crew.Walking).Append(" walking, ").Append(_crew.Visitors).Append(" visitor); ");
        }

        private void EndCast()
        {
            for (int i = 0; i < _members.Count; i++)
            {
                _members[i].Role.Exit();
            }
            _members.Clear();
            _walker.Exit();
            _walking = Doing.Away;
            _walkerStop = -1;
            for (int i = 0; i < _seats.Count; i++)
            {
                SeatPlace s = _seats[i];
                s.Held = false;
                _seats[i] = s;
            }
        }

        // The places the walking officer goes to, each with what they face there and do; the door last.
        private void BuildStops()
        {
            _stops.Clear();
            if (_hasCoffee)
            {
                AddStop(_coffeeFront, _coffeeFront + _lastCoffeeFace, null);
            }
            if (_hasBriefing)
            {
                AddStop(_briefingFront, _briefingFront + (_briefingFront - _briefingScreen), "talk_a");
            }
            if (HasCell)
            {
                AddStop(At(_cellBack - _cellDeep - 0.8f, _cellAside), CellSpot, SceneClips.TakeNotes);
            }
            for (int i = 0; i < _seats.Count; i++)
            {
                if (_seats[i].Use == SeatUse.Desk)
                {
                    AddStop(_seats[i].Stand + (_seats[i].Stand - _seats[i].Seat.position).normalized * 0.4f, _seats[i].Seat.position, "talk_a");
                    break;
                }
            }
            if (_board != null && _board.gameObject.activeSelf)
            {
                AddStop(_board.position + _board.forward * 1.0f, _board.position, null);
            }
            AddStop(_origin + _in * 0.8f, _origin - _in * 3f, null);   // the door: going off shift
        }

        private void AddStop(Vector3 at, Vector3 face, string clip)
        {
            if (Standable(at, out Vector3 spot))
            {
                _stops.Add(new Stop { At = spot, Face = face, Clip = clip });
            }
        }

        // ---------------------------------------------------------------- once a second

        private void CastTick(float now)
        {
            for (int i = 0; i < _members.Count; i++)
            {
                _members[i].Role.Tick(now);
            }
            _walker.Tick(now);
            if (!Settings.People)
            {
                return;
            }
            if (now >= _nextPeople)
            {
                _nextPeople = now + 6f;
                PostSergeant();
            }
            for (int i = 0; i < _members.Count; i++)
            {
                StepMember(_members[i], now);
            }
            if (_hasWalker)
            {
                Walk(now);
            }
        }

        // The sergeant behind the counter, the one who is talked to: a person the pool gives up for nobody.
        private void PostSergeant()
        {
            if (_sergeant.Present || !Standable(_sergeantAt, out Vector3 post))
            {
                return;
            }
            if (_sergeant.Enter(post, ActorLook.Officer, 0.5f, onRoad: false))
            {
                _sergeant.Stand(post - _in * 6f);
                if (HasDesk && ClipLibrary.Has(SceneClips.Clipboard))
                {
                    _sergeant.Work(SceneClips.Clipboard);
                }
            }
        }

        // Nobody comes in through the door while the player stands in it.
        private bool DoorClear()
        {
            PlayerController player = Helpers.PlayerHelper.PlayerController;
            return player == null || CitySpots.Flat(player.transform.position - _origin) > 2.5f * 2.5f;
        }

        private bool Spawn(ActorRole role, Vector3 at, ActorLook look, bool presence)
        {
            return presence ? role.EnterPresence(at, look, 0.8f, onRoad: false) : role.Enter(at, look, 0.8f, onRoad: false);
        }

        private int FreeSeat(SeatUse use)
        {
            int start = Random.Range(0, Mathf.Max(1, _seats.Count));
            for (int k = 0; k < _seats.Count; k++)
            {
                int i = (start + k) % _seats.Count;
                if (_seats[i].Use == use && !_seats[i].Held)
                {
                    return i;
                }
            }
            return -1;
        }

        private void Hold(int seat, bool held)
        {
            if (seat >= 0 && seat < _seats.Count)
            {
                SeatPlace s = _seats[seat];
                s.Held = held;
                _seats[seat] = s;
            }
        }

        // Put out where they sit and seated there and then (on the way in, under the fade).
        private bool SitDownAtOnce(Member m, float now)
        {
            int seat = FreeSeat(m.Use);
            if (seat < 0 && !(m.Use == SeatUse.Waiting && _hasVisitor))
            {
                return false;
            }
            Vector3 at = seat >= 0 ? _seats[seat].Stand : _visitorAt;
            if (!Spawn(m.Role, at, m.Look, m.Presence))
            {
                return false;
            }
            m.Seat = seat;
            Hold(seat, true);
            Settle(m, now);
            return true;
        }

        // At their seat: sat down on it (typing, at a desk), or for somebody waiting with no chair, standing by the bench.
        private void Settle(Member m, float now)
        {
            ActorRole role = m.Role;
            if (m.Seat >= 0)
            {
                Transform seat = _seats[m.Seat].Seat;
                if (m.Use == SeatUse.Desk && role.Knows(AnimationType.UsingDesktopComputer))
                {
                    role.Repeat(AnimationType.UsingDesktopComputer, 5f, 15f);   // typing, as the game's employees at a desk computer
                }
                if (!role.Sit(seat))
                {
                    role.Quiet();
                    role.Stand(seat.position + seat.forward * 2f);
                }
            }
            else
            {
                role.Stand(_visitorFace);
                if (ClipLibrary.Has("queue_wait"))
                {
                    role.Work("queue_wait");
                }
            }
            m.Doing = Doing.Seated;
            m.Until = now + (m.Use == SeatUse.Desk ? Random.Range(DeskStayMin, DeskStayMax)
                : m.Use == SeatUse.Break ? Random.Range(BreakStayMin, BreakStayMax) : Random.Range(WaitStayMin, WaitStayMax));
        }

        private void StepMember(Member m, float now)
        {
            ActorRole role = m.Role;
            if (m.Doing != Doing.Away && !role.Present)
            {
                Gone(m, now);   // taken back by the pool for a scene, or out of the door
                return;
            }
            switch (m.Doing)
            {
                case Doing.Away:
                    if (now >= m.Until)
                    {
                        ComeIn(m, now);
                    }
                    break;

                case Doing.Coming:
                    if (role.Settled || now >= m.Until)
                    {
                        Vector3 goal = m.Seat >= 0 ? _seats[m.Seat].Stand : _visitorAt;
                        if (CitySpots.Flat(role.Position - goal) < 1.2f * 1.2f || now >= m.Until)
                        {
                            Settle(m, now);
                        }
                    }
                    break;

                case Doing.Seated:
                    if (now >= m.Until)
                    {
                        GetUp(m, now);
                    }
                    break;

                case Doing.Errand:
                    if (!m.ErrandStarted && (role.Settled || now >= m.Until))
                    {
                        m.ErrandStarted = true;
                        m.Until = now + Random.Range(6f, 11f);
                        if (m.ErrandClip != null && ClipLibrary.Has(m.ErrandClip))
                        {
                            role.Work(m.ErrandClip);
                        }
                    }
                    else if (m.ErrandStarted && now >= m.Until)
                    {
                        if (m.ThenLeave || m.Seat < 0)
                        {
                            Leave(m, now);
                        }
                        else
                        {
                            role.Drop();
                            role.Go(_seats[m.Seat].Stand, Pace.Walk, _seats[m.Seat].Seat.position, 25f);
                            m.Doing = Doing.Coming;
                            m.Until = now + 25f;
                        }
                    }
                    break;
            }
        }

        // In through the door and over to a free seat of theirs.
        private void ComeIn(Member m, float now)
        {
            int seat = FreeSeat(m.Use);
            if (seat < 0 && !(m.Use == SeatUse.Waiting && _hasVisitor))
            {
                m.Until = now + 30f;
                return;
            }
            if (!DoorClear() || !Spawn(m.Role, _origin + _in * 0.5f, m.Look, m.Presence))
            {
                m.Until = now + (DoorClear() ? 15f : 5f);
                return;
            }
            m.Seat = seat;
            Hold(seat, true);
            Vector3 goal = seat >= 0 ? _seats[seat].Stand : _visitorAt;
            Vector3 face = seat >= 0 ? _seats[seat].Seat.position : _visitorFace;
            m.Role.Go(goal, Pace.Walk, face, 30f);
            m.Doing = Doing.Coming;
            m.Until = now + 30f;
        }

        // Up from the seat: an officer at a desk goes off shift; one on a break fetches a coffee (and now and then goes);
        // somebody waiting goes up to the side of the counter, has a word, and leaves.
        private void GetUp(Member m, float now)
        {
            m.Role.Quiet();
            switch (m.Use)
            {
                case SeatUse.Desk:
                    if (_hasCoffee && Random.value < 0.4f)
                    {
                        Errand(m, _coffeeFront, _coffeeFront + _lastCoffeeFace, null, false, now);
                    }
                    else
                    {
                        Leave(m, now);
                    }
                    break;

                case SeatUse.Break:
                    if (_hasCoffee && Random.value < 0.7f)
                    {
                        Errand(m, _coffeeFront, _coffeeFront + _lastCoffeeFace, null, Random.value < 0.3f, now);
                    }
                    else
                    {
                        Leave(m, now);
                    }
                    break;

                default:
                    Vector3 side = ServiceSpot + _right * (AsideOf(ServiceSpot) <= 0f ? 0.95f : -0.95f) - _in * 0.25f;
                    if (HasDesk && Standable(side, out Vector3 spot))
                    {
                        Errand(m, spot, _sergeantAt, "talk_a", true, now);
                    }
                    else
                    {
                        Leave(m, now);
                    }
                    break;
            }
        }

        private void Errand(Member m, Vector3 at, Vector3 face, string clip, bool thenLeave, float now)
        {
            m.ErrandAt = at;
            m.ErrandFace = face;
            m.ErrandClip = clip;
            m.ErrandStarted = false;
            m.ThenLeave = thenLeave;
            m.Role.Drop();
            m.Role.Go(at, Pace.Walk, face, 25f);
            m.Doing = Doing.Errand;
            m.Until = now + 25f;
        }

        private void Leave(Member m, float now)
        {
            Hold(m.Seat, false);
            m.Seat = -1;
            m.Role.Quiet();
            m.Role.Drop();
            m.Role.WalkOff(_origin, Pace.Walk, 30f);
            m.Doing = Doing.Leaving;
            m.Until = now + 30f;
        }

        private void Gone(Member m, float now)
        {
            m.Role.Exit();
            Hold(m.Seat, false);
            m.Seat = -1;
            m.Doing = Doing.Away;
            m.Until = now + (m.Use == SeatUse.Waiting ? Random.Range(VisitorGapMin, VisitorGapMax) : Random.Range(ShiftGapMin, ShiftGapMax));
        }

        // ---------------------------------------------------------------- the officer walking the rooms

        private void Walk(float now)
        {
            if (_walking != Doing.Away && !_walker.Present)
            {
                _walker.Exit();
                _walking = Doing.Away;
                _walkerUntil = now + Random.Range(ShiftGapMin, ShiftGapMax);   // off shift; somebody else comes on later
                return;
            }
            switch (_walking)
            {
                case Doing.Away:
                    if (now >= _walkerUntil && DoorClear())
                    {
                        if (Spawn(_walker, _origin + _in * 0.5f, ActorLook.Officer, true))
                        {
                            GoToStop(Random.Range(0, _stops.Count - 1), now);   // on shift: in through the door
                        }
                        else
                        {
                            _walkerUntil = now + 15f;
                        }
                    }
                    break;

                case Doing.Coming:
                    if (_walker.Settled || now >= _walkerUntil)
                    {
                        ArriveAtStop(_walkerStop, now);
                    }
                    break;

                case Doing.Errand:
                    if (now >= _walkerUntil)
                    {
                        _walker.Drop();
                        int next = Random.value < 0.15f ? _stops.Count - 1 : Random.Range(0, _stops.Count - 1);
                        if (next == _walkerStop)
                        {
                            next = (next + 1) % (_stops.Count - 1);
                        }
                        if (next == _stops.Count - 1)
                        {
                            _walker.WalkOff(_origin, Pace.Walk, 30f);   // off shift, through the door
                            _walking = Doing.Leaving;
                        }
                        else
                        {
                            GoToStop(next, now);
                        }
                    }
                    break;
            }
        }

        private void GoToStop(int stop, float now)
        {
            _walkerStop = Mathf.Clamp(stop, 0, _stops.Count - 1);
            _walker.Go(_stops[_walkerStop].At, Pace.Walk, _stops[_walkerStop].Face, 30f);
            _walking = Doing.Coming;
            _walkerUntil = now + 30f;
        }

        private void ArriveAtStop(int stop, float now)
        {
            _walkerStop = Mathf.Clamp(stop, 0, _stops.Count - 1);
            Stop s = _stops[_walkerStop];
            _walker.Stand(s.Face);
            if (s.Clip != null && ClipLibrary.Has(s.Clip))
            {
                _walker.Work(s.Clip);
            }
            _walking = Doing.Errand;
            _walkerUntil = now + Random.Range(8f, 16f);
        }
    }
}
