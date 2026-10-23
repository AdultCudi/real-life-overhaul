using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>A suspect running along the pavement with an officer a few strides behind: caught, lost, or brought down by a fall.</summary>
    internal sealed class FootChaseScene : CrimeScene
    {
        private const int Running = 0, Closing = 1, GivingUp = 2, OnPhone = 3, Fallen = 4;

        private readonly Knockdown _fall = new Knockdown();
        private Vector3 _far;

        public override string Id => "foot_chase";
        public override Vector3 Focus => Cop.Present ? Cop.Position : Scene;

        protected override bool Stage(in CrimeSetup setup, float now)
        {
            if (!StreetSpots.PavementAround(setup.Spot, 55f, 85f, false, out _far))
            {
                return false;
            }
            Vector3 back = setup.Spot - _far;
            back.y = 0f;
            if (!First.Enter(setup.Spot, ActorLook.Civilian, 3f, onRoad: false)
                || !Cop.Enter(setup.Spot + back.normalized * 9f, ActorLook.Officer, 6f, onRoad: false))
            {
                return false;
            }
            First.Go(_far, RunnerPace, _far, 45f);
            Catchable = First;   // the player can end the chase by getting in the way
            Cop.Go(First.Position, Pace.Run, _far, 10f);
            Go(Running, now, 9f, 16f);
            return true;
        }

        protected override void Run(float now, Vector3 player)
        {
            switch (Step)
            {
                case Running:
                    if (!First.Present || !Cop.Present)
                    {
                        End();   // one of the two could not stay on the street
                    }
                    else if (now < StepEnd && !First.Settled)
                    {
                        Pursue(Cop, First, Pace.Run, now);
                    }
                    else if (Is("tripped") && Trip(now))
                    {
                        Go(Fallen, now, 12f);
                    }
                    else if (Is("escaped"))
                    {
                        if (First.Settled)
                        {
                            First.WalkAwayFrom(Cop.Position, Pace.Run, 40f, 20f);
                        }
                        else
                        {
                            First.WalkOff(_far, Pace.Run, 30f);
                        }
                        Go(GivingUp, now, 3f, 5f);
                    }
                    else
                    {
                        if (Is("tripped"))
                        {
                            Ending = Entry.Ending("caught") ?? Ending;   // nobody fell after all
                        }
                        First.Stand(Cop.Position);
                        First.Surrender();
                        Go(Closing, now, 12f);
                    }
                    break;

                case Fallen:
                    Pursue(Cop, First, Pace.Run, now, 1.8f);
                    if (_fall.Tick(now))
                    {
                        if (!First.Present)
                        {
                            End();
                            break;
                        }
                        Go(Closing, now, 8f);
                    }
                    break;

                case Closing:
                    if (!First.Present)
                    {
                        End();
                    }
                    else if (Cop.Present && now < StepEnd && !Within(Cop, First, 2.3f))
                    {
                        Pursue(Cop, First, Pace.Run, now);
                    }
                    else
                    {
                        Arrest(First, now);
                    }
                    break;

                case GivingUp:
                    if (now < StepEnd)
                    {
                        Pursue(Cop, First, Pace.Jog, now);
                    }
                    else
                    {
                        Cop.Stand(First.Position);   // lost them: the officer calls it in
                        Cop.Hold(PhonePose, PhoneProp);
                        Conclude();
                        Go(OnPhone, now, 8f, 12f);
                    }
                    break;

                case OnPhone:
                    if (now >= StepEnd)
                    {
                        Leave(now, player);
                    }
                    break;
            }
        }

        private bool Trip(float now)
        {
            if (!_fall.Begin(First, Cop.Position, now, 4f))
            {
                return false;
            }
            Catchable = null;   // on the ground: the officer has them
            return true;
        }

        protected override void Release()
        {
            _fall.Clear();
        }
    }
}
