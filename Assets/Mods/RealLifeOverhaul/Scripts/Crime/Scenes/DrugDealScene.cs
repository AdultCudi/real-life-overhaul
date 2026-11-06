using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>Something changing hands: an officer breaks it up (one may be caught) or finds nothing, or nobody comes.</summary>
    internal sealed class DrugDealScene : CrimeScene
    {
        private const int Dealing = 0, CopComing = 1, Scatter = 2, Word = 3, Lingering = 4;

        public override string Id => "drug_deal";

        protected override bool Stage(in CrimeSetup setup, float now)
        {
            Vector3 apart = Crime.RandomHeading() * 0.55f;
            if (!First.Enter(setup.Spot + apart, ActorLook.Civilian, 2f, onRoad: false)
                || !Second.Enter(setup.Spot - apart, ActorLook.Civilian, 2f, onRoad: false))
            {
                return false;
            }
            First.Stand(Second.Position);
            Second.Stand(First.Position);
            First.Repeat(Reach, 4f, 7f);
            Second.Repeat(Reach, 5f, 8f);
            Go(Dealing, now, Is("done") ? 16f : 6f, Is("done") ? 26f : 12f);
            return true;
        }

        public override bool Report(float now)
        {
            if (Active && !Reported && Step == Dealing && Is("done"))
            {
                Ending = Entry.Ending("scattered") ?? Ending;   // the call sends the officer nobody else had sent
                StepEnd = now;
            }
            Reported = Active;
            return Active;
        }

        protected override void Run(float now, Vector3 player)
        {
            switch (Step)
            {
                case Dealing:
                    if (!First.Present || !Second.Present)
                    {
                        End();
                    }
                    else if (now >= StepEnd)
                    {
                        if (!Is("done") && SendOfficer(Scene, 22f, 32f))
                        {
                            Go(CopComing, now, 34f);
                        }
                        else
                        {
                            Part(now, "done");
                        }
                    }
                    break;

                case CopComing:
                    if (!Cop.Present)
                    {
                        Part(now, "done");
                    }
                    else if (!Is("nothing") && Within(Cop, First, 14f))
                    {
                        First.Quiet();   // the officer is seen coming: one runs each way
                        Second.Quiet();
                        RunFor(First, Cop.Position);
                        Flee(Second, First.Position);
                        Go(Scatter, now, 5f, 9f);
                    }
                    else if (Cop.Settled || now >= StepEnd)
                    {
                        First.Quiet();
                        Second.Quiet();
                        Cop.Stand(Scene);
                        First.Face(Cop.Position);
                        Second.Face(Cop.Position);
                        First.Repeat(Arguing, 3f, 5f, CharacterEmojiName.CommonYellowNeutral, 0.5f);
                        Go(Word, now, 9f, 14f);
                    }
                    break;

                case Scatter:
                    if (!First.Present)
                    {
                        Cop.Stand(Scene);
                        Conclude("scattered");
                        Go(Lingering, now, 4f);
                    }
                    else if (now < StepEnd)
                    {
                        Pursue(Cop, First, Pace.Run, now);
                    }
                    else if (Is("one_caught") && Cop.Present)
                    {
                        Arrest(First, now);
                    }
                    else
                    {
                        First.WalkAwayFrom(Cop.Position, Pace.Run, 40f, 15f);
                        Cop.Stand(First.Position);
                        Cop.Hold(PhonePose, PhoneProp);
                        Conclude("scattered");
                        Go(Lingering, now, 6f, 9f);
                    }
                    break;

                case Word:
                    if (now >= StepEnd)
                    {
                        Part(now, "nothing");
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

        private void Part(float now, string ending)
        {
            First.Quiet();
            Second.Quiet();
            First.WalkAwayFrom(Second.Position, Pace.Walk);
            Second.WalkAwayFrom(First.Position, Pace.Walk);
            Conclude(ending);
            Go(Lingering, now, 3f, 5f);
        }
    }
}
