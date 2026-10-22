using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// A fight that ends with one of them on the ground: the puncher is arrested, runs, is pulled away by a passer-by, or
    /// both walk off.
    /// </summary>
    internal sealed class AssaultScene : CrimeScene
    {
        private const int Squaring = 0, Down = 1, Standoff = 2, Aftermath = 3;

        private readonly Knockdown _fall = new Knockdown();

        public override string Id => "assault";

        protected override bool Stage(in CrimeSetup setup, float now)
        {
            Vector3 apart = Crime.RandomHeading() * 0.6f;
            if (!First.Enter(setup.Spot + apart, ActorLook.Civilian, 2f, onRoad: false)
                || !Second.Enter(setup.Spot - apart, ActorLook.Civilian, 2f, onRoad: false))
            {
                return false;
            }
            First.Stand(Second.Position);
            Second.Stand(First.Position);
            First.Repeat(Arguing, 2.5f, 4f, CharacterEmojiName.CommonRedAngry, 0.7f);
            Second.Repeat(Arguing, 3f, 4.5f, CharacterEmojiName.CommonRedAngry, 0.6f);
            if (Is("arrested"))
            {
                SendOfficer(setup.Spot, 28f, 38f);
            }
            else if (Is("pulled_apart"))
            {
                if (StreetSpots.PavementAround(setup.Spot, 10f, 16f, false, out Vector3 side)
                    && Third.Enter(side, ActorLook.Civilian, 3f, onRoad: false))
                {
                    Third.Stand(setup.Spot);
                }
            }
            Go(Squaring, now, 8f, 14f);
            return true;
        }

        protected override void Run(float now, Vector3 player)
        {
            switch (Step)
            {
                case Squaring:
                    if (!First.Present || !Second.Present)
                    {
                        End();
                    }
                    else if (now >= StepEnd)
                    {
                        First.Quiet();
                        Second.Quiet();
                        First.Gesture(Punch);
                        _fall.Begin(Second, First.Position, now, 3.4f);
                        Say(NotificationType.Warning, Entry.Extra, 6f);
                        if (Is("fled"))
                        {
                            RunFor(First, Second.Position);
                        }
                        else if (Is("pulled_apart") && Third.Present)
                        {
                            Third.Go(Crime.Short(First.Position, Third.Position, 1.2f), Pace.Run, First.Position, 10f);
                        }
                        Go(Down, now, 14f);
                    }
                    break;

                case Down:
                    if (!_fall.Tick(now))
                    {
                        break;
                    }
                    if (Second.Present)
                    {
                        Second.Face(First.Position);
                        Second.Gesture(Arguing);
                    }
                    if (Is("fled"))
                    {
                        Second.Hold(PhonePose, PhoneProp);
                        Conclude();
                        Go(Aftermath, now, 7f, 10f);
                    }
                    else
                    {
                        Go(Standoff, now, Is("arrested") ? 22f : 5f);
                    }
                    break;

                case Standoff:
                    if (Is("arrested") && Cop.Present)
                    {
                        if (Cop.Settled || now >= StepEnd)
                        {
                            Arrest(First, now);
                        }
                        break;
                    }
                    if (now >= StepEnd)
                    {
                        First.WalkAwayFrom(Second.Position, Pace.Walk);   // no officer came
                        Second.WalkAwayFrom(First.Position, Pace.Walk);
                        Conclude(Is("pulled_apart") && Third.Present ? "pulled_apart" : "walked_off");
                        Go(Aftermath, now, 4f);
                    }
                    break;

                case Aftermath:
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
