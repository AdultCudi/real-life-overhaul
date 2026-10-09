using Helpers;
using UI.Notification;
using UnityEngine;
using Random = UnityEngine.Random;

namespace RealLifeOverhaul
{
    /// <summary>
    /// Trouble in a shop the player is standing in, played by the mod's own people so that no
    /// customer, employee or sale is touched (customers only get a startled face over their
    /// heads). Two kinds:
    ///   a shoplifter (somebody else's shop): conceals something and makes for the door; gets
    ///     out, is met at the door by the store detective, turns out to have simply forgotten
    ///     to pay, or runs into the player, who was standing in the way;
    ///   a hold-up (any shop with customers): a robber comes in, holds the room for a few
    ///     seconds and runs; police meet them outside, or arrive too late, or the robber's
    ///     nerve goes first. In the player's own shop a hold-up that succeeds costs a small,
    ///     capped amount from the till, and only if crimes against the player are switched on.
    /// </summary>
    internal sealed class ShopIncidentScene : CrimeScene
    {
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
                // In through the door and straight to the middle of the floor.
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
                            // Whoever is waiting for them is already in the doorway.
                            Cop.Enter(_door, ActorLook.Officer, 2f, onRoad: false);
                            Cop.Stand(First.Position);
                        }
                        // Whoever the staff are shouting after hurries for the door, as the line just shown says: the one who
                        // only forgot to pay at a trot, the thief flat out.
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
                    // Somebody standing squarely in a running thief's way.
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
                        // Flat on the floor at the player's feet. An officer is fetched from the street to take them.
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
                // Met in the doorway: cuffed, and led out.
                Arrest(First, now);
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

            // Through the door and gone.
            First.WalkOff(_outside, Pace.Run, 15f);
            if (_holdUp && _ownShop && Settings.CrimeAgainstPlayer && !Is("bungled"))
            {
                int taken = Crime.Rob(0.04f, 40, 500, "reallife_transaction_robbery");
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
