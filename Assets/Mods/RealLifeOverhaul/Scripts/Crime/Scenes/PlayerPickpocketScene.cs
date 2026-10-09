using Helpers;
using UI.Notification;
using UnityEngine;
using Random = UnityEngine.Random;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The player is the mark. Someone comes up behind them on the pavement, brushes past and
    /// takes a modest share of the cash they carry (never more than they have, and capped).
    /// Sometimes the player feels it: the thief jogs off with a marker over their head, and
    /// catching up with them gets the money back. Sometimes they do not, and find out a little
    /// later; even then, walking into the thief before they are gone gets it back.
    /// </summary>
    internal sealed class PlayerPickpocketScene : CrimeScene
    {
        private const int Approach = 0, Chase = 1, Unnoticed = 2, Caught = 3;
        private const float Share = 0.06f;
        private const int Least = 10;
        private const int Most = 250;
        private const float GrabReach = 1.5f;
        private const float CatchReach = 2.3f;

        private int _taken;
        private float _liftedAt;

        public override string Id => "player_pickpocket";
        public override Vector3 Focus => First.Present ? First.Position : Scene;
        public override bool ShowsMarker => Step == Chase;
        protected override float MaxTime => 80f;

        protected override bool Stage(in CrimeSetup setup, float now)
        {
            if (PlayerHelper.IsUsingVehicle || Crime.Cash < Least * 3
                || !StreetSpots.PavementAround(setup.Player, 9f, 17f, true, out Vector3 from)
                || !First.Enter(from, ActorLook.Civilian, 3f, onRoad: true))
            {
                return false;
            }
            _taken = 0;
            First.Go(setup.Player, Pace.Jog, setup.Player, 10f);
            Go(Approach, now, 22f);
            return true;
        }

        public override bool Report(float now)
        {
            return false;   // by the time anyone could come it is over
        }

        protected override void Run(float now, Vector3 player)
        {
            if (!First.Present)
            {
                if (_taken > 0)
                {
                    Lost();
                }
                End();
                return;
            }
            float gapSqr = (First.Position - player).sqrMagnitude;
            switch (Step)
            {
                case Approach:
                    if (PlayerHelper.IsUsingVehicle || now >= StepEnd)
                    {
                        First.WalkAwayFrom(player, Pace.Walk);   // the chance has gone; just another pedestrian
                        Go(StepLeaving, now, 20f);
                    }
                    else if (gapSqr < GrabReach * GrabReach)
                    {
                        Lift(now, player);
                    }
                    else if (now >= NextRepath)
                    {
                        NextRepath = now + RepathInterval;
                        First.Go(player, gapSqr > 36f ? Pace.Jog : Pace.Walk, player, 10f);
                    }
                    break;

                case Chase:
                    if (gapSqr < CatchReach * CatchReach && now - _liftedAt > 2f)
                    {
                        Recover(now, player);
                    }
                    else if (now >= StepEnd || First.Settled)
                    {
                        First.WalkAwayFrom(player, Pace.Run, 40f, 12f);
                        Lost();
                        Go(StepLeaving, now, 14f);
                    }
                    break;

                case Unnoticed:
                    if (gapSqr < GrabReach * GrabReach && now - _liftedAt > 4f)
                    {
                        Recover(now, player);   // walked right into them
                    }
                    else if (now >= StepEnd)
                    {
                        Lost();
                        First.WalkAwayFrom(player, Pace.Walk, 30f, 10f);
                        Go(StepLeaving, now, 12f);
                    }
                    break;

                case Caught:
                    if (now >= StepEnd)
                    {
                        First.Quiet();
                        Flee(First, player);
                        Go(StepLeaving, now, 15f);
                    }
                    break;
            }
        }

        private void Lift(float now, Vector3 player)
        {
            First.Gesture(Reach);
            _taken = Crime.Rob(Share, Least, Most, "reallife_transaction_pickpocket");
            _liftedAt = now;
            if (_taken <= 0)
            {
                First.WalkAwayFrom(player, Pace.Walk);
                Go(StepLeaving, now, 20f);
                return;
            }
            if (!StreetSpots.AwayFrom(First.Position, player, 70f, onRoad: false, out Vector3 far))
            {
                far = First.Position;
            }
            if (Is("noticed"))
            {
                // Felt it. They are quick, but not quicker than someone who wants their wallet back.
                First.Go(far, Pace.Jog, far, 30f);
                ConcludeAmount("noticed", _taken, NotificationType.Error);
                Go(Chase, now, 26f);
            }
            else
            {
                First.Go(far, Pace.Walk, far, 40f);   // (Go, not WalkOff: they must still be there to be walked into)
                Go(Unnoticed, now, 14f, 24f);
            }
        }

        private void Recover(float now, Vector3 player)
        {
            Crime.Give(_taken, "reallife_transaction_recovered");
            ConcludeAmount("recovered", _taken, NotificationType.Success);
            ModState.AddModifier(ModState.MoodUp);
            _taken = 0;
            if (Random.value < 0.5f)
            {
                HoldForPolice(First, now, player);   // held for an officer; the base class takes it from here
                return;
            }
            First.Stand(player);
            First.Surrender();
            Go(Caught, now, 2.5f);
        }

        private void Lost()
        {
            if (_taken <= 0)
            {
                return;
            }
            ConcludeAmount(Step == Unnoticed ? "unnoticed" : "lost", _taken, NotificationType.Error);
            ModState.AddModifier(ModState.Robbed);
            _taken = 0;
        }

        // Cut short with the money still gone (the player went indoors, the scene timed out): they are at least told.
        protected override void Release()
        {
            if (_taken > 0 && SaveGameManager.Current != null)
            {
                Crime.ShowAmount(NotificationType.Error, "reallife_crime_pickpocket_gone", _taken);
                _taken = 0;
            }
        }
    }
}
