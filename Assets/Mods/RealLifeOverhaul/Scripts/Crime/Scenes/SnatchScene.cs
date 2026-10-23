using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>A grab and a sprint: the victim chases, a passer-by brings the thief down, an officer runs them in, or it was nothing.</summary>
    internal sealed class SnatchScene : CrimeScene
    {
        private const int Approach = 0, Grab = 1, Running = 2, Tackled = 3, Cornered = 4, HandBack = 5, Aftermath = 6;

        private readonly Knockdown _fall = new Knockdown();
        private Vector3 _far;
        private bool _copChasing;

        public override string Id => "snatch";
        public override Vector3 Focus => First.Present ? First.Position : Scene;

        protected override bool Stage(in CrimeSetup setup, float now)
        {
            if (!StreetSpots.PavementAround(setup.Spot, 45f, 70f, false, out _far)
                || !Second.Enter(setup.Spot, ActorLook.Civilian, 3f, onRoad: false))
            {
                return false;
            }
            Vector3 behind = Second.Position - _far;
            behind.y = 0f;
            if (!First.Enter(Second.Position + behind.normalized * 9f, ActorLook.Civilian, 5f, onRoad: false))
            {
                return false;
            }
            Second.Stand(_far);
            Second.Hold(TextPose, PhoneProp);
            First.Go(Crime.Short(Second.Position, First.Position, 0.9f), Pace.Walk, Second.Position, 14f);

            if (Is("bystander"))   // the passer-by who steps in, further along the thief's way out
            {
                Third.Enter(Vector3.Lerp(Second.Position, _far, 0.35f), ActorLook.Civilian, 4f, onRoad: false);
                Third.Stand(Second.Position);
            }
            _copChasing = false;
            Go(Approach, now, 15f);
            return true;
        }

        public override bool Report(float now)
        {
            if (Active && !Reported && Step <= Running && !Cop.Present && SendOfficer(First.Position, 14f, 22f))
            {
                _copChasing = true;   // the call put an officer on the thief's heels
            }
            Reported = Active;
            return Active;
        }

        protected override void Run(float now, Vector3 player)
        {
            switch (Step)
            {
                case Approach:
                    if (!First.Present || !Second.Present)
                    {
                        End();
                    }
                    else if (First.Settled || now >= StepEnd)
                    {
                        First.Gesture(Reach);
                        Go(Grab, now, 0.9f);
                    }
                    break;

                case Grab:
                    if (now < StepEnd)
                    {
                        break;
                    }
                    Second.Drop();
                    if (Is("false_alarm"))
                    {
                        Second.Stand(First.Position);   // tapped on the shoulder: they had dropped something
                        Second.Emote(CharacterEmojiName.CommonGreenHappy);
                        Conclude();
                        Go(Aftermath, now, 4f, 6f);
                        break;
                    }
                    Second.Stand(_far);
                    Second.Gesture(Despair);
                    Second.Emote(CharacterEmojiName.CommonRedAngry, 3f);
                    First.Go(_far, RunnerPace, _far, 40f);
                    Catchable = First;
                    Say(NotificationType.Warning, Entry.Extra, 6f);
                    if (Is("officer") && SendOfficer(First.Position, 14f, 24f))
                    {
                        _copChasing = true;
                    }
                    if (Is("victim_chase"))
                    {
                        Second.Go(First.Position, Pace.Run, _far, 10f);
                    }
                    Go(Running, now, 7f, 12f);
                    break;

                case Running:
                    RunChase(now, player);
                    break;

                case Tackled:
                    if (_fall.Tick(now))
                    {
                        if (!First.Present)
                        {
                            End();
                        }
                        else if (Cop.Present || SendOfficer(First.Position, 12f, 20f))
                        {
                            First.Surrender();
                            Go(Cornered, now, 14f);
                        }
                        else
                        {
                            Flee(First, Third.Position);   // nobody to hand them to
                            Conclude("escaped");
                            Go(Aftermath, now, 5f);
                        }
                    }
                    break;

                case Cornered:
                    if (!First.Present)
                    {
                        End();
                    }
                    else if (Cop.Present && now < StepEnd && !Within(Cop, First, 2.3f))
                    {
                        Pursue(Cop, First, Pace.Run, now);
                    }
                    else if (Cop.Present && Ending != null && !Ending.Arrest)
                    {
                        Arrest(First, now, "officer");   // an officer nobody had rolled: the call brought them
                    }
                    else if (Cop.Present)
                    {
                        Arrest(First, now);
                    }
                    else
                    {
                        Flee(First, Second.Position);
                        Conclude("escaped");
                        Go(Aftermath, now, 5f);
                    }
                    break;

                case HandBack:
                    if (now >= StepEnd)
                    {
                        First.Quiet();
                        Flee(First, Second.Position, 40f);
                        Second.Emote(CharacterEmojiName.CommonGreenHappy);
                        Conclude();
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

        private void RunChase(float now, Vector3 player)
        {
            if (!First.Present)
            {
                Conclude("escaped");
                Second.Stand(_far);
                Second.Hold(PhonePose, PhoneProp);
                Go(Aftermath, now, 6f, 9f);
                return;
            }
            if (Is("bystander") && Third.Present && Within(First, Third, 3.2f))
            {
                Third.Gesture(Punch);
                Catchable = null;
                if (!_fall.Begin(First, Third.Position, now, 3.2f))
                {
                    First.Stand(Third.Position);   // grabbed by the collar instead of floored
                }
                Go(Tackled, now, 14f);
                return;
            }
            if (Is("victim_chase") && Second.Present)
            {
                Pursue(Second, First, Pace.Run, now);
            }
            else if (_copChasing && Cop.Present)
            {
                Pursue(Cop, First, Pace.Run, now);
            }
            if (now < StepEnd && !First.Settled)
            {
                return;
            }

            if (_copChasing && Cop.Present)
            {
                First.Stand(Cop.Position);
                First.Surrender();
                Go(Cornered, now, 12f);
            }
            else if (Is("victim_chase") && Second.Present)
            {
                First.Stand(Second.Position);   // caught up with: a tug of war, and the thief lets go
                Second.Stand(First.Position);
                First.Repeat(Arguing, 1.5f, 2.5f);
                Second.Repeat(Arguing, 1.5f, 2.5f, CharacterEmojiName.CommonRedAngry, 0.7f);
                Go(HandBack, now, 4f, 6f);
            }
            else
            {
                if (First.Settled)
                {
                    First.WalkAwayFrom(Second.Position, Pace.Run, 40f, 20f);
                }
                else
                {
                    First.WalkOff(_far, Pace.Run, 25f);
                }
                Conclude("escaped");
                Second.Stand(_far);
                Second.Hold(PhonePose, PhoneProp);
                Go(Aftermath, now, 6f, 9f);
            }
        }

        protected override void Release()
        {
            _fall.Clear();
        }
    }
}
