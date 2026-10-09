using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// An officer on foot writing a ticket for one of the game's parked cars. The car is only
    /// stood beside, never touched. The owner may come running: too late, in time to argue, or
    /// in time to be let off.
    /// </summary>
    internal sealed class ParkingTicketScene : CrimeScene
    {
        private const int Writing = 0, Owner = 1, Argument = 2, Lingering = 3;

        private Vector3 _car;

        public override string Id => "parking_ticket";

        protected override bool Stage(in CrimeSetup setup, float now)
        {
            if (!CrimeCars.FindParked(setup.Spot, 30f, out _car, out Vector3 forward, out float halfWidth))
            {
                return false;
            }
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            if (!StreetSpots.OnPavement(_car + right * (halfWidth + 0.8f), 1.3f, out Vector3 spot)
                && !StreetSpots.OnPavement(_car - right * (halfWidth + 0.8f), 1.3f, out spot))
            {
                return false;
            }
            if (!Cop.Enter(spot, ActorLook.Officer, 2f, onRoad: false))
            {
                return false;
            }
            Scene = Cop.Position;
            Cop.Stand(_car);
            Cop.Work(SceneClips.Clipboard, TextPose, PhoneProp);   // the ticket book: the clipboard clip, or texting where that cannot be had
            Go(Writing, now, Is("ticketed") ? 14f : 6f, Is("ticketed") ? 22f : 10f);
            return true;
        }

        protected override void Run(float now, Vector3 player)
        {
            if (!Cop.Present && Step < Lingering)
            {
                End();
                return;
            }
            switch (Step)
            {
                case Writing:
                    if (now < StepEnd)
                    {
                        break;
                    }
                    if (!Is("ticketed") && StreetSpots.PavementAround(Scene, 16f, 26f, true, out Vector3 from)
                        && Second.Enter(from, ActorLook.Civilian, 3f, onRoad: false))
                    {
                        Second.Go(Crime.Short(Scene, Second.Position, 1.5f), Pace.Run, Scene, 22f);
                        Go(Owner, now, 24f);
                    }
                    else
                    {
                        Cop.Drop();
                        Conclude("ticketed");
                        Go(Lingering, now, 2f, 4f);
                    }
                    break;

                case Owner:
                    if (Second.Settled || !Second.Present || now >= StepEnd)
                    {
                        Cop.Drop();
                        Cop.Stand(Second.Position);
                        Second.Repeat(Arguing, 2f, 3.5f, CharacterEmojiName.CommonRedAngry, 0.6f);
                        Go(Argument, now, 7f, 12f);
                    }
                    break;

                case Argument:
                    if (now >= StepEnd)
                    {
                        Second.Quiet();
                        if (Is("let_off"))
                        {
                            Second.Emote(CharacterEmojiName.CommonGreenHappy);
                        }
                        else
                        {
                            Second.Gesture(Despair);
                        }
                        Conclude();
                        Go(Lingering, now, 3f, 5f);
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
