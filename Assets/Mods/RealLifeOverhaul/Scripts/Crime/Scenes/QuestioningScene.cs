using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>An officer and a civilian, face to face; the civilian is doing most of the talking. Let go, led off, or gone in a sprint.</summary>
    internal sealed class QuestioningScene : CrimeScene
    {
        private const int Talking = 0, Lingering = 1, Bolting = 2;

        public override string Id => "questioning";

        protected override bool Stage(in CrimeSetup setup, float now)
        {
            if (!Cop.Enter(setup.Spot, ActorLook.Officer, 3f, onRoad: false)
                || !First.Enter(setup.Spot + Crime.RandomHeading() * 1.6f, ActorLook.Civilian, 2f, onRoad: false))
            {
                return false;
            }
            Cop.Stand(First.Position);
            First.Stand(Cop.Position);
            First.Repeat(Arguing, 4f, 7f, CharacterEmojiName.CommonYellowNeutral, 0.4f);
            Go(Talking, now, 25f, 45f);
            return true;
        }

        protected override void Run(float now, Vector3 player)
        {
            switch (Step)
            {
                case Talking:
                    if (now < StepEnd)
                    {
                        break;
                    }
                    First.Quiet();
                    if (Is("led_off") && Cop.Present)
                    {
                        Arrest(First, now);
                    }
                    else if (Is("bolted") && Cop.Present)
                    {
                        RunFor(First, Cop.Position);
                        Go(Bolting, now, 4f, 6f);
                    }
                    else
                    {
                        // Free to go: the civilian walks off, the officer watches them a moment.
                        First.Emote(CharacterEmojiName.CommonGreenHappy);
                        First.WalkAwayFrom(Cop.Position, Pace.Walk);
                        Conclude("released");
                        Go(Lingering, now, 3f, 5f);
                    }
                    break;

                case Bolting:
                    if (now < StepEnd && First.Present)
                    {
                        Pursue(Cop, First, Pace.Run, now);
                    }
                    else
                    {
                        Cop.Stand(First.Position);
                        Cop.Hold(PhonePose, PhoneProp);
                        Conclude();
                        Go(Lingering, now, 7f, 10f);
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
    }
}
