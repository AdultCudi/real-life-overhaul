using GleyTrafficSystem;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>How a lone offender is put on the street.</summary>
    internal enum OffenderStyle
    {
        AtWall,        // facing a wall, busy with it (graffiti)
        AtParkedCar,   // at the door of one of the game's parked cars (a break-in, a keyed door)
        Drunk,         // swaying on the pavement
        Pitch,         // standing where they have set up (a vendor, a panhandler, a busker)
        Stopped        // already face to face with an officer (a fare dodger)
    }

    /// <summary>
    /// One person doing something they should not on the pavement. Several crimes share its endings, each choosing its
    /// own in the catalogue: with an officer ran, caught, arrested, moved_on, ticketed, cleared; without one told_off (a
    /// passer-by), friend (taken home), collapsed, alone.
    /// </summary>
    internal sealed class LoneOffenderScene : CrimeScene
    {
        private const int Busy = 0, CopComing = 1, Word = 2, Chase = 3, Visitor = 4, Fallen = 5, Lingering = 6;

        private readonly string _id;
        private readonly OffenderStyle _style;
        private readonly Knockdown _fall = new Knockdown();
        private Vector3 _faces;
        private bool _writing;

        public LoneOffenderScene(string id, OffenderStyle style)
        {
            _id = id;
            _style = style;
        }

        public override string Id => _id;

        protected override bool Stage(in CrimeSetup setup, float now)
        {
            Vector3 spot = setup.Spot;
            _writing = false;
            _faces = spot + Crime.RandomHeading();
            switch (_style)
            {
                case OffenderStyle.AtParkedCar:
                    if (!CrimeCars.FindParked(spot, 30f, out Vector3 centre, out Vector3 forward, out float halfWidth))
                    {
                        return false;
                    }
                    Vector3 right = Vector3.Cross(Vector3.up, forward);
                    if (!StreetSpots.OnPavement(centre + right * (halfWidth + 0.7f), 1.2f, out spot)
                        && !StreetSpots.OnPavement(centre - right * (halfWidth + 0.7f), 1.2f, out spot))
                    {
                        return false;
                    }
                    _faces = centre;   // at the car's pavement side
                    break;

                case OffenderStyle.AtWall:
                    // Away from the road is near enough towards the buildings.
                    Waypoint lane = TrafficTools.Ready ? TrafficTools.ClosestWaypoint(spot, 30f) : null;
                    if (lane != null)
                    {
                        Vector3 away = spot - lane.position;
                        away.y = 0f;
                        if (away.sqrMagnitude > 0.25f)
                        {
                            _faces = spot + away.normalized * 3f;
                        }
                    }
                    break;
            }

            if (!First.Enter(spot, ActorLook.Civilian, 2f, onRoad: false))
            {
                return false;
            }
            Scene = First.Position;
            First.Stand(_faces);
            switch (_style)
            {
                case OffenderStyle.AtWall:
                case OffenderStyle.AtParkedCar:
                    First.Repeat(Reach, 2.2f, 4f);
                    break;
                case OffenderStyle.Drunk:
                    First.Hold(DrunkPose);
                    First.Repeat(Arguing, 5f, 9f, CharacterEmojiName.CommonYellowNeutral, 0.5f);
                    break;
                case OffenderStyle.Pitch:
                    First.Repeat(Reach, 5f, 9f);
                    break;
                case OffenderStyle.Stopped:
                    if (!Cop.Enter(spot + Crime.RandomHeading() * 1.6f, ActorLook.Officer, 2f, onRoad: false))
                    {
                        return false;
                    }
                    Cop.Stand(First.Position);
                    First.Stand(Cop.Position);
                    First.Repeat(Arguing, 3.5f, 6f, CharacterEmojiName.CommonYellowNeutral, 0.4f);
                    Go(Word, now, 12f, 22f);
                    return true;
            }

            if (NeedsOfficer)
            {
                Go(Busy, now, 6f, 14f);   // the officer is sent later, so the offender is seen at it first
            }
            else if (Is("told_off") || Is("friend"))
            {
                Go(Busy, now, 8f, 16f);
            }
            else
            {
                Go(Busy, now, 18f, 30f);
            }
            return true;
        }

        private bool NeedsOfficer => Is("ran") || Is("caught") || Is("arrested") || Is("moved_on") || Is("ticketed") || Is("cleared");

        public override bool Report(float now)
        {
            if (Active && !Reported && Step == Busy && !Cop.Present)
            {
                StepEnd = now;   // whoever was coming comes now
                if (!NeedsOfficer && SendOfficer(Scene, 18f, 28f))   // nobody was: the call sends someone, and that changes the ending
                {
                    Ending = Entry.Ending("moved_on") ?? Entry.Ending("cleared") ?? Ending;
                    Go(CopComing, now, 34f);
                }
            }
            Reported = Active;
            return Active;
        }

        protected override void Run(float now, Vector3 player)
        {
            if (!First.Present && Step < Lingering && Step != Chase && !_fall.Down)
            {
                End();
                return;
            }
            switch (Step)
            {
                case Busy:
                    if (now < StepEnd)
                    {
                        break;
                    }
                    if (NeedsOfficer && SendOfficer(Scene, 20f, 32f))
                    {
                        Go(CopComing, now, 34f);
                    }
                    else if ((Is("told_off") || Is("friend")) && StreetSpots.PavementAround(Scene, 14f, 22f, true, out Vector3 from)
                             && Third.Enter(from, ActorLook.Civilian, 3f, onRoad: false))
                    {
                        Third.Go(Crime.Short(Scene, Third.Position, 1.4f), Is("friend") ? Pace.Walk : Pace.Jog, Scene, 26f);
                        Go(Visitor, now, 28f);
                    }
                    else if (Is("collapsed") && _fall.Begin(First, _faces, now, 1.6f))
                    {
                        Go(Fallen, now, 14f);
                    }
                    else
                    {
                        First.Quiet();
                        First.Drop();
                        First.WalkAwayFrom(player, Pace.Walk);
                        Conclude("alone");
                        Go(Lingering, now, 3f);
                    }
                    break;

                case CopComing:
                    if (!Cop.Present)
                    {
                        Go(Busy, now, 0f);
                        Ending = Entry.Ending("alone");
                        break;
                    }
                    if ((Is("ran") || Is("caught")) && Within(Cop, First, 13f))
                    {
                        First.Quiet();
                        First.Drop();
                        RunFor(First, Cop.Position);
                        Go(Chase, now, 5f, 9f);
                    }
                    else if (Cop.Settled || now >= StepEnd)
                    {
                        Cop.Stand(First.Position);
                        First.Quiet();
                        First.Stand(Cop.Position);
                        First.Repeat(Arguing, 3.5f, 6f, CharacterEmojiName.CommonYellowNeutral, 0.4f);
                        Go(Word, now, Is("ticketed") ? 14f : 7f, Is("ticketed") ? 20f : 12f);
                    }
                    break;

                case Word:
                    if (Is("ticketed") && !_writing && now >= StepEnd - 6f)
                    {
                        _writing = true;
                        Cop.Hold(TextPose, PhoneProp);   // writing the ticket
                    }
                    if (now < StepEnd)
                    {
                        break;
                    }
                    First.Quiet();
                    Cop.Drop();
                    if (Is("arrested") && Cop.Present)
                    {
                        Arrest(First, now);
                        break;
                    }
                    if (Is("cleared"))
                    {
                        First.Emote(CharacterEmojiName.CommonGreenHappy);
                    }
                    else
                    {
                        First.Drop();
                        First.WalkAwayFrom(Cop.Position, Pace.Walk);
                    }
                    Conclude();
                    Go(Lingering, now, 3f, 5f);
                    break;

                case Chase:
                    if (!First.Present)
                    {
                        Cop.Stand(Scene);
                        Conclude("ran");
                        Go(Lingering, now, 4f);
                    }
                    else if (now < StepEnd)
                    {
                        Pursue(Cop, First, Pace.Run, now);
                    }
                    else if (Is("caught") && Cop.Present)
                    {
                        Arrest(First, now);
                    }
                    else
                    {
                        First.WalkAwayFrom(Cop.Position, Pace.Run, 40f, 15f);
                        Cop.Stand(First.Position);
                        Cop.Hold(PhonePose, PhoneProp);
                        Conclude("ran");
                        Go(Lingering, now, 6f, 9f);
                    }
                    break;

                case Visitor:
                    if (!Third.Settled && Third.Present && now < StepEnd)
                    {
                        break;
                    }
                    First.Quiet();
                    First.Drop();
                    if (Is("friend") && Third.Present)
                    {
                        if (StreetSpots.AwayFrom(Scene, player, 30f, onRoad: false, out Vector3 home))
                        {
                            First.WalkOff(home, Pace.Walk, 28f);
                            Third.WalkOff(home, Pace.Walk, 28f);
                        }
                    }
                    else
                    {
                        Third.Stand(First.Position);
                        Third.Repeat(Arguing, 1.5f, 2.5f, CharacterEmojiName.CommonRedAngry, 0.8f);
                        First.WalkAwayFrom(Third.Position, _style == OffenderStyle.Drunk ? Pace.Walk : Pace.Jog);
                    }
                    if (Third.Present)
                    {
                        Conclude();
                    }
                    else
                    {
                        Conclude("alone");
                    }
                    Go(Lingering, now, 4f, 6f);
                    break;

                case Fallen:
                    if (_fall.Tick(now))
                    {
                        First.WalkAwayFrom(player, Pace.Walk);
                        Conclude();
                        Go(Lingering, now, 3f);
                    }
                    break;

                case Lingering:
                    if (now >= StepEnd)
                    {
                        Leave(now, player);
                    }
                    break;
            }
        }

        protected override void Release()
        {
            _fall.Clear();
        }
    }
}
