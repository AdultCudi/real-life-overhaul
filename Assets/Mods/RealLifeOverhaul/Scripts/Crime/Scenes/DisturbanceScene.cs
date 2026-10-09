using Helpers;
using UnityEngine;
using Random = UnityEngine.Random;

namespace RealLifeOverhaul
{
    /// <summary>Two people having a row on the pavement. An officer may walk up; the player stepping in takes the heat out of it.</summary>
    internal sealed class DisturbanceScene : CrimeScene
    {
        private const int Quarrel = 0, Calming = 1, Lingering = 2;

        public override string Id => "disturbance";

        protected override bool Stage(in CrimeSetup setup, float now)
        {
            Vector3 apart = Crime.RandomHeading() * 0.85f;
            if (!First.Enter(setup.Spot + apart, ActorLook.Civilian, 2f, onRoad: false)
                || !Second.Enter(setup.Spot - apart, ActorLook.Civilian, 2f, onRoad: false))
            {
                return false;
            }
            First.Stand(Second.Position);
            Second.Stand(First.Position);
            First.Repeat(Arguing, 3.5f, 6.5f, CharacterEmojiName.CommonRedAngry, 0.6f);
            Second.Repeat(Arguing, 4f, 7f, CharacterEmojiName.CommonRedAngry, 0.5f);

            // With no officer to be had, they simply run out of steam.
            bool officer = !Is("fizzled") && SendOfficer(setup.Spot, 22f, 34f);
            Go(Quarrel, now, officer ? 38f : Random.Range(20f, 30f));
            return true;
        }

        protected override void OnNear(Vector3 player)
        {
            // Stepping in between two strangers is the decent thing to do.
            if (Step == Quarrel && !PlayerHelper.IsUsingVehicle)
            {
                ModState.AddModifier(ModState.Helped);
                base.OnNear(player);
            }
        }

        protected override void Run(float now, Vector3 player)
        {
            switch (Step)
            {
                case Quarrel:
                    if (Cop.Settled || now >= StepEnd)
                    {
                        First.Quiet();
                        Second.Quiet();
                        if (Cop.Present)
                        {
                            Cop.Stand(Scene);
                            First.Face(Cop.Position);
                            Second.Face(Cop.Position);
                        }
                        Go(Calming, now, 4f, 6f);
                    }
                    break;

                case Calming:
                    if (now < StepEnd)
                    {
                        break;
                    }
                    if (Is("arrested") && Cop.Present)
                    {
                        Second.WalkAwayFrom(First.Position, Pace.Walk);
                        Arrest(First, now);
                        break;
                    }
                    // Sent their separate ways.
                    First.WalkAwayFrom(Second.Position, Pace.Walk);
                    Second.WalkAwayFrom(First.Position, Pace.Walk);
                    Conclude(Cop.Present ? "calmed" : "fizzled");
                    Go(Lingering, now, 3f, 5f);
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
