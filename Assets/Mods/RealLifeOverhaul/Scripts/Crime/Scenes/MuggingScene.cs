using Helpers;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// A mugging: one person with their hands up, another far too close to them. The mugger gets
    /// away with it, is knocked flat by a victim who had had enough, runs into an officer, or
    /// bolts because the player walked up.
    /// </summary>
    internal sealed class MuggingScene : CrimeScene
    {
        private const int Threat = 0, Getaway = 1, Floored = 2, CopChase = 3, Aftermath = 4;

        private readonly Knockdown _fall = new Knockdown();

        public override string Id => "mugging";

        protected override bool Stage(in CrimeSetup setup, float now)
        {
            if (!Second.Enter(setup.Spot, ActorLook.Civilian, 3f, onRoad: false)
                || !First.Enter(setup.Spot + Crime.RandomHeading() * 1.3f, ActorLook.Civilian, 2f, onRoad: false))
            {
                return false;
            }
            First.Stand(Second.Position);
            Second.Stand(First.Position);
            Second.Surrender();
            First.Repeat(Reach, 3f, 5f, CharacterEmojiName.CommonRedAngry, 0.5f);
            if (Is("interrupted") || Is("lost"))
            {
                SendOfficer(setup.Spot, 24f, 34f);
            }
            Go(Threat, now, 9f, 15f);
            return true;
        }

        // Somebody walking straight at them is a witness too many.
        protected override void OnNear(Vector3 player)
        {
            if (Step != Threat || PlayerHelper.IsUsingVehicle)
            {
                return;
            }
            First.Quiet();
            Second.Quiet();
            RunFor(First, player);
            Second.Emote(CharacterEmojiName.CommonGreenHappy);
            ModState.AddModifier(ModState.Helped);
            Say(NotificationType.Success, Entry.Near, 8f);
            Go(Aftermath, Time.time, 4f);
        }

        public override bool Report(float now)
        {
            if (Active && !Reported && Step == Threat)
            {
                SendOfficer(Scene, 18f, 26f);
            }
            Reported = Active;
            return Active;
        }

        protected override void Run(float now, Vector3 player)
        {
            switch (Step)
            {
                case Threat:
                    if (!First.Present || !Second.Present)
                    {
                        End();
                        break;
                    }
                    if (Cop.Present && (Cop.Settled || Within(Cop, First, 9f)))
                    {
                        // Seen. The mugger runs and the officer goes after them.
                        First.Quiet();
                        Second.Quiet();
                        RunFor(First, Cop.Position);
                        Go(CopChase, now, 6f, 10f);
                        break;
                    }
                    if (now < StepEnd || (Cop.Present && !Cop.Settled && now < StepEnd + 12f))
                    {
                        break;
                    }
                    First.Quiet();
                    Second.Quiet();
                    if (Is("fought_back") && Floor(now))
                    {

                        Go(Floored, now, 14f);
                    }
                    else
                    {
                        First.Gesture(Reach);
                        RunFor(First, Second.Position);
                        Second.Gesture(Despair);
                        Go(Getaway, now, 3f, 5f);
                    }
                    break;

                case Getaway:
                    if (now >= StepEnd)
                    {
                        Second.Hold(PhonePose, PhoneProp);
                        Conclude("robbed");
                        Go(Aftermath, now, 7f, 10f);
                    }
                    break;

                case Floored:
                    if (_fall.Tick(now))
                    {
                        RunFor(First, Second.Position);
                        Second.Emote(CharacterEmojiName.CommonGreenHappy);
                        Conclude("fought_back");
                        Go(Aftermath, now, 5f);
                    }
                    break;

                case CopChase:
                    if (!First.Present)
                    {
                        Conclude("lost");
                        Cop.Stand(Second.Position);
                        Go(Aftermath, now, 5f);
                    }
                    else if (now < StepEnd)
                    {
                        Pursue(Cop, First, Pace.Run, now);
                    }
                    else if (Is("lost"))
                    {
                        First.WalkAwayFrom(Cop.Position, Pace.Run, 40f, 15f);
                        Cop.Stand(First.Position);
                        Cop.Hold(PhonePose, PhoneProp);
                        Conclude("lost");
                        Go(Aftermath, now, 7f);
                    }
                    else
                    {
                        Arrest(First, now, "interrupted");
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

        // The victim swings. False if nobody can be knocked down just now: then it goes the mugger's way.
        private bool Floor(float now)
        {
            Second.Gesture(Punch);
            return _fall.Begin(First, Second.Position, now, 3f);
        }

        protected override void Release()
        {
            _fall.Clear();
        }
    }
}
