using System;
using System.Collections.Generic;
using BAModAPI;
using BigAmbitions.Characters;
using UnityEngine;
using Random = UnityEngine.Random;

namespace RealLifeOverhaul.FitOut
{
    /// <summary>
    /// The people of a fitted-out floor, as many as <see cref="FitCrew"/> gives within the mod's people to spare: staff
    /// typing at desks, a pair at a meeting table, somebody in the lounge or waiting, one or two walking between stops;
    /// now and then one leaves and another comes in. Out only while the player is in the room (all at once under the black
    /// screen, otherwise through the door). Ticked once a second by the caller: a few comparisons a person.
    /// </summary>
    internal sealed class FitCast
    {
        public const int Reserve = 3;                 // the mod's people always left for anything else

        /// <summary>A place to sit on the fitted floor, what it is for, and where somebody stands to sit on it.</summary>
        public struct SeatPlace
        {
            public Transform At;
            public FitUse Use;
            public Vector3 Stand;
        }

        private enum Doing { Away, Coming, Seated, Walking, Pausing, Leaving }

        private sealed class Member
        {
            public readonly ActorRole Role = new ActorRole();
            public FitUse Use;          // the seat's use; None for one who walks about
            public bool Presence;       // only to be seen: gives way to a scene
            public int Seat = -1;
            public int Stop = -1;
            public Doing Doing;
            public float Until;
        }

        private readonly IModLogger _logger;
        private readonly List<Member> _members = new List<Member>(12);
        private readonly List<SeatPlace> _seats = new List<SeatPlace>(64);
        private readonly List<Vector3> _stops = new List<Vector3>(24);
        private bool[] _held = new bool[0];
        private Vector3 _door;

        public FitCast(IModLogger logger)
        {
            _logger = logger;
        }

        /// <summary>The crew as it was given, for the log.</summary>
        public FitCrew Crew { get; private set; }

        /// <summary>The people of a floor that stands now: some there at once if the screen is still black, the rest through the door.</summary>
        public void Begin(FitKind kind, List<SeatPlace> seats, List<Vector3> stops, Vector3 door)
        {
            End();
            _seats.AddRange(seats);
            _stops.AddRange(stops);
            _held = new bool[_seats.Count];
            _door = door;
            int spare = ActorPool.Instance != null ? ActorPool.Instance.Free - Reserve : 0;
            Crew = FitCrew.For(kind, Count(FitUse.Desk), Count(FitUse.Meeting), Count(FitUse.Lounge), Count(FitUse.Waiting), _stops.Count, spare);
            Add(Crew.AtDesks, FitUse.Desk, false);
            Add(Crew.InMeeting, FitUse.Meeting, false);
            Add(Crew.InLounge, FitUse.Lounge, true);
            Add(Crew.Waiting, FitUse.Waiting, true);
            Add(Crew.Walking, FitUse.None, true);
            float now = Time.time;
            bool hidden = SceneFinds.Hidden;
            for (int i = 0; i < _members.Count; i++)
            {
                Member m = _members[i];
                m.Doing = Doing.Away;
                m.Until = now + (hidden ? 0f : 1f + i * 1.5f);
                if (hidden)
                {
                    Arrive(m, now, atOnce: true);   // already at work when the player comes in
                }
            }
        }

        private void Add(int count, FitUse use, bool presence)
        {
            for (int i = 0; i < count; i++)
            {
                _members.Add(new Member { Use = use, Presence = presence });
            }
        }

        private int Count(FitUse use)
        {
            int n = 0;
            for (int i = 0; i < _seats.Count; i++)
            {
                n += _seats[i].Use == use ? 1 : 0;
            }
            return n;
        }

        /// <summary>Once a second while the player is in the room.</summary>
        public void Tick(float now)
        {
            bool arrived = false;
            for (int i = 0; i < _members.Count; i++)
            {
                Member m = _members[i];
                m.Role.Tick(now);
                if (!Settings.People)
                {
                    Leave(m, now, at: false);
                    continue;
                }
                if (!m.Role.Present)
                {
                    if (m.Doing != Doing.Away)
                    {
                        Release(m);
                        m.Doing = Doing.Away;   // gone, or taken back by the pool: somebody else later
                        m.Until = now + Random.Range(25f, 70f);
                    }
                    if (!arrived && now >= m.Until && DoorClear())
                    {
                        arrived = Arrive(m, now, atOnce: false);
                        if (!arrived)
                        {
                            m.Until = now + Random.Range(10f, 25f);
                        }
                    }
                    continue;
                }
                Step(m, now);
            }
        }

        // Seated at once under the black screen, or in through the door and on to a seat or a stop.
        private bool Arrive(Member m, float now, bool atOnce)
        {
            if (m.Use == FitUse.None)
            {
                if (_stops.Count < 2)
                {
                    return false;
                }
                int stop = Random.Range(0, _stops.Count);
                Vector3 from = atOnce ? _stops[stop] : _door;
                if (!Spawn(m, from))
                {
                    return false;
                }
                m.Stop = stop;
                if (atOnce)
                {
                    m.Role.Stand(_stops[(stop + 1) % _stops.Count]);
                    m.Doing = Doing.Pausing;
                    m.Until = now + Random.Range(2f, 8f);
                }
                else
                {
                    m.Role.Go(_stops[stop], Pace.Walk, _stops[(stop + 1) % _stops.Count]);
                    m.Doing = Doing.Walking;
                }
                return true;
            }
            int seat = FreeSeat(m.Use);
            if (seat < 0 || !Spawn(m, atOnce ? _seats[seat].Stand : _door))
            {
                return false;
            }
            m.Seat = seat;
            _held[seat] = true;
            if (atOnce)
            {
                SitDown(m, now);
            }
            else
            {
                m.Role.Go(_seats[seat].Stand, Pace.Walk, _seats[seat].At.position);
                m.Doing = Doing.Coming;
                m.Until = now + 40f;
            }
            return true;
        }

        private bool Spawn(Member m, Vector3 near)
        {
            return m.Presence ? m.Role.EnterPresence(near, ActorLook.Civilian, 0.8f, onRoad: false) : m.Role.Enter(near, ActorLook.Civilian, 0.8f, onRoad: false);
        }

        // Typing at a desk, as the game's employees do; sitting back anywhere else.
        private void SitDown(Member m, float now)
        {
            SeatPlace seat = _seats[m.Seat];
            if (m.Use == FitUse.Desk && m.Role.Knows(AnimationType.UsingDesktopComputer))
            {
                m.Role.Repeat(AnimationType.UsingDesktopComputer, 5f, 15f);
            }
            else
            {
                m.Role.Quiet();
            }
            if (!m.Role.Sit(seat.At))
            {
                m.Role.Quiet();
                m.Role.Stand(seat.At.position + seat.At.forward * 2f);
            }
            m.Doing = Doing.Seated;
            float stay = m.Use == FitUse.Desk ? Random.Range(180f, 420f) : m.Use == FitUse.Meeting ? Random.Range(100f, 240f) : Random.Range(50f, 140f);
            m.Until = now + stay;
        }

        private void Step(Member m, float now)
        {
            switch (m.Doing)
            {
                case Doing.Coming:
                    if (m.Role.Settled)
                    {
                        SitDown(m, now);
                    }
                    else if (now >= m.Until)
                    {
                        Leave(m, now, at: false);   // could not get there
                    }
                    break;
                case Doing.Seated:
                    if (now < m.Until)
                    {
                        break;
                    }
                    if (m.Use == FitUse.Desk && Random.value < 0.6f)
                    {
                        m.Until = now + Random.Range(120f, 300f);   // back to work
                        break;
                    }
                    Leave(m, now, at: true);
                    break;
                case Doing.Walking:
                    if (m.Role.Settled)
                    {
                        m.Doing = Doing.Pausing;
                        m.Until = now + Random.Range(4f, 12f);
                    }
                    break;
                case Doing.Pausing:
                    if (now < m.Until)
                    {
                        break;
                    }
                    if (Random.value < 0.12f)
                    {
                        Leave(m, now, at: true);   // off home, or out to a meeting
                        break;
                    }
                    int next = (m.Stop + 1 + Random.Range(0, Math.Max(1, _stops.Count - 1))) % _stops.Count;
                    m.Stop = next;
                    m.Role.Go(_stops[next], Pace.Walk, _stops[(next + 1) % _stops.Count]);
                    m.Doing = Doing.Walking;
                    break;
                case Doing.Leaving:
                    if (now >= m.Until)
                    {
                        m.Role.Exit();   // the way out took too long
                    }
                    break;
            }
        }

        // Off through the door (walking), or taken away at once.
        private void Leave(Member m, float now, bool at)
        {
            Release(m);
            if (!m.Role.Present)
            {
                return;
            }
            if (at)
            {
                m.Role.WalkOff(_door, Pace.Walk);
                m.Doing = Doing.Leaving;
                m.Until = now + 30f;
            }
            else
            {
                m.Role.Exit();
                m.Doing = Doing.Away;
                m.Until = now + Random.Range(25f, 70f);
            }
        }

        private void Release(Member m)
        {
            if (m.Seat >= 0 && m.Seat < _held.Length)
            {
                _held[m.Seat] = false;
            }
            m.Seat = -1;
        }

        private int FreeSeat(FitUse use)
        {
            int start = Random.Range(0, Math.Max(1, _seats.Count));
            for (int k = 0; k < _seats.Count; k++)
            {
                int i = (start + k) % _seats.Count;
                if (_seats[i].Use == use && !_held[i] && _seats[i].At != null)
                {
                    return i;
                }
            }
            return -1;
        }

        // Nobody comes in through the door while the player stands in it.
        private bool DoorClear()
        {
            PlayerController player = Helpers.PlayerHelper.PlayerController;
            return player == null || CitySpots.Flat(player.transform.position - _door) > 2.5f * 2.5f;
        }

        /// <summary>Everybody taken away. Safe at any time.</summary>
        public void End()
        {
            for (int i = 0; i < _members.Count; i++)
            {
                try
                {
                    _members[i].Role.Exit();
                }
                catch (Exception e)
                {
                    _logger?.Error(e);
                }
            }
            _members.Clear();
            _seats.Clear();
            _stops.Clear();
            _held = new bool[0];
        }
    }
}
