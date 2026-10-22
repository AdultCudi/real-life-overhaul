using Helpers;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// Trouble in the shop the player stands in, played by the mod's own people (customers only look startled). A
    /// shoplifter in somebody else's shop makes for the door: gets out, is met there, only forgot to pay, or runs into the
    /// player. A robber holds the room up and runs: met outside, too late, or losing their nerve; in the player's own shop
    /// a success costs a capped amount, with crimes against you on.
    /// </summary>
    internal sealed class ShopIncidentScene : CrimeScene
    {
        internal const float HoldUpShare = 0.04f;                // of the cash carried
        internal const int HoldUpLeast = 40, HoldUpMost = 500;   // dollars at the start's prices
        private const int Entering = 0, Busy = 1, Running = 2, AtDoor = 3, Floored = 4, Out = 5;

        private readonly string _id;
        private readonly bool _holdUp;
        private readonly Knockdown _fall = new Knockdown();
        private BuildingManager _building;
        private Vector3 _door;
        private Vector3 _outside;
        private bool _ownShop;

        public ShopIncidentScene(string id, bool holdUp)
        {
            _id = id;
            _holdUp = holdUp;
        }

        public override string Id => _id;
        public override bool Indoors => true;
        public override Vector3 Focus => First.Present ? First.Position : Scene;
        protected override float MaxTime => 80f;

        protected override bool Stage(in CrimeSetup setup, float now)
        {
            _building = InstanceBehavior<BuildingManager>.Instance;
            _ownShop = setup.Place == CrimePlace.OwnShop;
            if (!ShopFloor.Door(setup.Player, out _door, out _outside))
            {
                return false;
            }
            if (_holdUp)
            {
                if (!ShopFloor.RandomSpot(out Vector3 floor) || !First.Enter(_door, ActorLook.Civilian, 2f, onRoad: false))
                {
                    return false;
                }
                Scene = floor;
                First.Go(floor, Pace.Run, setup.Player, 12f);
                Go(Entering, now, 13f);
                return true;
            }

            Vector3 standAt, faces;
            if (!ShopFloor.RandomShelf(out standAt, out faces, out _, out _))
            {
                if (!ShopFloor.RandomSpot(out standAt))
                {
                    return false;
                }
                faces = standAt + Crime.RandomHeading();
            }
            if (!First.Enter(standAt, ActorLook.Civilian, 1.5f, onRoad: false))
            {
                return false;
            }
            Scene = First.Position;
            First.Stand(faces);
            First.Repeat(Reach, 1.6f, 2.6f);
            Go(Busy, now, 4f, 7f);
            return true;
        }

        public override bool Report(float now)
        {
            if (Active && !Reported && _holdUp && Step <= Busy)
            {
                Ending = Entry.Ending("caught_outside") ?? Ending;   // the call gets a car to the door in time
            }
            Reported = Active;
            return Active;
        }

        protected override void Run(float now, Vector3 player)
        {
            if (!BuildingManager.IsInsideBuilding || InstanceBehavior<BuildingManager>.Instance != _building)
            {
                End();   // the player has left the shop
                return;
            }
            switch (Step)
            {
                case Entering:
                    if (!First.Present)
                    {
                        End();
                    }
                    else if (First.Settled || now >= StepEnd)
                    {
                        First.Stand(player);
                        First.Repeat(Reach, 1.8f, 3f, CharacterEmojiName.CommonRedAngry, 0.7f);
                        ShopFloor.StartleCustomers(CharacterEmojiName.CommonRedAngry, 5);
                        Say(NotificationType.Error, Entry.Extra, 7f);
                        Go(Busy, now, Is("bungled") ? 3f : 8f, Is("bungled") ? 5f : 14f);
                    }
                    break;

                case Busy:
                    if (!First.Present)
                    {
                        End();
                    }
                    else if (now >= StepEnd)
                    {
                        First.Quiet();
                        if (!_holdUp)
                        {
                            ShopFloor.StartleCustomers(CharacterEmojiName.CommonYellowNeutral, 3);
                            Say(NotificationType.Warning, Entry.Extra, 6f);
                        }
                        bool met = Is("caught_at_door") || Is("caught_outside");
                        if (met)
                        {
                            Cop.Enter(_door, ActorLook.Officer, 2f, onRoad: false);   // already in the doorway
                            Cop.Stand(First.Position);
                        }
                        // For the door: one who only forgot to pay at a trot, the thief flat out.
                        First.Go(_door, Is("false_alarm") ? Pace.Jog : Pace.Run, _outside, 30f);
                        Go(Running, now, 31f);
                    }
                    break;

                case Running:
                    if (!First.Present)
                    {
                        End();
                        break;
                    }
                    if (!_holdUp && !Is("false_alarm") && !PlayerHelper.IsUsingVehicle && (First.Position - player).sqrMagnitude < 1.5f * 1.5f)
                    {
                        _fall.Begin(First, player, now, 3f);
                        Go(Floored, now, 14f);
                        break;
                    }
                    if (First.Settled || now >= StepEnd)
                    {
                        AtTheDoor(now, player);
                    }
                    break;

                case Floored:
                    if (_fall.Tick(now))
                    {
                        // On the floor at the player's feet: an officer is fetched from the street.
                        if (First.Present && Cop.Enter(_door, ActorLook.Officer, 2f, onRoad: false))
                        {
                            Arrest(First, now, null, byPlayer: true);
                        }
                        else
                        {
                            ModState.AddModifier(ModState.Helped);
                            Conclude("tripped");
                            Go(AtDoor, now, 4f);
                        }
                    }
                    break;

                case AtDoor:
                    if (now >= StepEnd)
                    {
                        First.Quiet();
                        First.WalkOff(_outside, Pace.Walk, 20f);
                        Go(Out, now, 21f);
                    }
                    break;

                case Out:
                    if ((!First.Present) || now >= StepEnd)
                    {
                        End();
                    }
                    break;
            }
        }

        private void AtTheDoor(float now, Vector3 player)
        {
            if (Cop.Present)
            {
                Arrest(First, now);   // met in the doorway
                return;
            }
            if (Is("false_alarm"))
            {
                First.Stand(Scene);
                First.Gesture(Despair);
                Conclude();
                Go(AtDoor, now, 3f);
                return;
            }

            First.WalkOff(_outside, Pace.Run, 15f);
            if (_holdUp && _ownShop && Settings.CrimeAgainstPlayer && !Is("bungled"))
            {
                int taken = Crime.Rob(HoldUpShare, HoldUpLeast, HoldUpMost, "reallife_transaction_robbery");
                if (taken > 0)
                {
                    ModState.AddModifier(ModState.Robbed);
                    ConcludeAmount("till", taken, NotificationType.Error);
                    Go(Out, now, 16f);
                    return;
                }
            }
            Conclude(Is("bungled") ? "bungled" : _holdUp ? "fled" : "escaped");
            Go(Out, now, 16f);
        }

        protected override Vector3 IndoorExit => _outside;

        // Thanked by the shop, not by a passer-by.
        protected override void RewardCatch()
        {
            ModState.AddModifier(ModState.Helped);
            Conclude("tripped");
        }

        protected override void Release()
        {
            _fall.Clear();
            _building = null;
        }
    }
}
