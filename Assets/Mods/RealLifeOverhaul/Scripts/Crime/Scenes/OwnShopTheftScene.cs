using System;
using Localizor;
using UI.Notification;
using UnityEngine;
using UnityEngine.AI;
using Random = UnityEngine.Random;

namespace RealLifeOverhaul
{
    /// <summary>
    /// A theft the game has just booked in one of the player's own shops, acted out while they
    /// are inside. The game's simulation stays the only thing that takes stock and writes the
    /// loss: this scene is started by the director when it sees the shop's stolen value go up,
    /// and plays a thief (one of the mod's own people, so no customer and no sale is touched)
    /// concealing goods at a shelf and making for the door.
    ///   - Shelf covered by a camera: the player is told at once and the thief is marked.
    ///   - A guard on the floor: he goes after some thieves by himself.
    ///   - Neither: the player only knows if they were close and looking.
    /// Walking up and pressing the interaction key confronts the thief (drops it and runs, pays
    /// up, or bolts); a 911 call puts an officer at the door. A thief who is stopped takes the
    /// booked amount off the shop's stolen value again, never below zero and never after the
    /// day's books have closed. One who gets out costs exactly what the game had already booked.
    /// </summary>
    internal sealed class OwnShopTheftScene : CrimeScene
    {
        private const int Concealing = 0, Leaving = 1, Stopped = 2;
        private const float ConfrontReach = 3f;

        private BuildingRegistration _shop;
        private SecurityGuardEmployee _guard;   // ours to steer only while _guardHeld
        private bool _guardHeld;
        private float _amount;
        private int _day;
        private Vector3 _door;
        private Vector3 _outside;
        private bool _noticed;
        private bool _prompted;
        private bool _copWanted;
        private bool _confronted;
        private bool _heldByPlayer;
        private bool _outAsked;
        private float _copAt;

        public override string Id => "shop_theft_own";
        public override bool Indoors => true;
        public override bool ShowsMarker => _noticed;
        public override Vector3 Focus => First.Present ? First.Position : Scene;
        public override bool Interactive => Step <= Leaving && !_confronted;   // whether or not anyone has told the player: sharp eyes count
        protected override float MaxTime => 90f;

        protected override bool Stage(in CrimeSetup setup, float now)
        {
            _shop = ShopFloor.OwnRegistration;
            if (_shop == null || setup.Amount <= 0f
                || !ShopFloor.RandomShelf(out Vector3 standAt, out Vector3 shelf, out bool covered, out string shelfName)
                || !ShopFloor.Door(standAt, out _door, out _outside)
                || !First.Enter(standAt, ActorLook.Civilian, 1.5f, onRoad: false))
            {
                return false;
            }
            _amount = setup.Amount;
            _day = setup.Forced ? -1 : TimeHelper.CurrentDay;   // a staged test books nothing, so it takes nothing off the books either
            _guard = null;
            _guardHeld = false;
            _prompted = false;
            _copWanted = false;
            _confronted = false;
            _heldByPlayer = false;
            _outAsked = false;
            Scene = First.Position;
            First.Stand(shelf);
            First.Repeat(Reach, 1.6f, 2.6f);

            // Who knows about it.
            _noticed = false;
            if (covered)
            {
                _noticed = true;
                var data = new System.Collections.Generic.Dictionary<string, string>(1)
                {
                    { "shelf", string.IsNullOrEmpty(shelfName) ? string.Empty : shelfName.GetLocalization() }
                };
                Notifications.Show(NotificationType.Warning, "reallife_crime_shoptheft_camera", data, 9f);
            }
            else if ((setup.Player - Scene).sqrMagnitude < 8f * 8f && StreetSpots.IsInView(Scene))
            {
                _noticed = true;
                Crime.Show(NotificationType.Warning, "reallife_crime_shoptheft_spotted");
            }
            if (Is("guard") || setup.Forced)
            {
                _guard = ShopFloor.Guard();
            }
            Go(Concealing, now, 4f, 7f);
            return true;
        }

        public override bool Report(float now)
        {
            if (Active && !Reported && Step <= Leaving)
            {
                _copWanted = true;
                _copAt = now + Random.Range(5f, 9f);
                _noticed = true;
            }
            Reported = Active;
            return Active;
        }

        public override void Interact(float now, Vector3 player)
        {
            if (!Interactive || !First.Present || (First.Position - player).sqrMagnitude > ConfrontReach * ConfrontReach)
            {
                return;
            }
            _confronted = true;   // one confrontation: what they do about it is settled now
            _noticed = true;
            FreeGuard();
            First.Quiet();
            float roll = Random.value;
            if (_copWanted || Cop.Present)
            {
                // The police are on their way and the player is standing over them: they stay put.
                First.Stand(player);
                First.Surrender();
                Crime.Show(NotificationType.Success, "reallife_crime_shoptheft_held", 7f);
                _heldByPlayer = true;
                Go(Leaving, now, 40f);
            }
            else if (roll < 0.45f)
            {
                // Everything on the floor, and out of the door at a run.
                Recover("dropped");
                First.Gesture(Despair);
                First.Go(_outside, Pace.Run, _outside, 20f);
                Go(Stopped, now, 20f);
            }
            else if (roll < 0.75f)
            {
                Recover("paid");
                First.Stand(player);
                First.Gesture(Reach);
                Go(Stopped, now, 3f);
            }
            else
            {
                First.Go(_outside, Pace.Run, _outside, 20f);
                Say(NotificationType.Warning, Entry.Extra, 6f);
                Go(Leaving, now, 20f);
            }
        }

        protected override Vector3 IndoorExit => _outside;

        protected override void Run(float now, Vector3 player)
        {
            if (ShopFloor.OwnRegistration != _shop)
            {
                End();   // the player has left the shop
                return;
            }
            if (_copWanted && now >= _copAt && !Cop.Present && First.Present)
            {
                _copWanted = false;
                if (Cop.Enter(_door, ActorLook.Officer, 2f, onRoad: false))
                {
                    Cop.Stand(First.Position);
                }
            }
            if (_noticed && !_prompted && !_confronted && Step <= Leaving && (First.Position - player).sqrMagnitude < 5f * 5f)
            {
                _prompted = true;
                Crime.ShowKey(NotificationType.Info, "reallife_crime_shoptheft_prompt", KeyAction.Interact, 6f);
            }

            switch (Step)
            {
                case Concealing:
                    if (!First.Present)
                    {
                        End();
                    }
                    else if (now >= StepEnd)
                    {
                        First.Quiet();
                        First.Go(_outside, _noticed ? Pace.Jog : Pace.Walk, _outside, 45f);
                        if (_guard != null)
                        {
                            HoldGuard();
                        }
                        Go(Leaving, now, 46f);
                    }
                    break;

                case Leaving:
                    if (!First.Present)
                    {
                        End();
                        break;
                    }
                    if (Cop.Present && (Within(Cop, First, 3f) || _heldByPlayer))
                    {
                        if (!Within(Cop, First, 3f))
                        {
                            Pursue(Cop, First, Pace.Jog, now);   // to the thief the player is holding
                            break;
                        }
                        // Cuffed and led out through the door. The goods stay.
                        Recover("arrested");
                        if (Reported)
                        {
                            Crime.ThankCaller();
                        }
                        Arrest(First, now, string.Empty);
                        break;
                    }
                    if (_heldByPlayer)
                    {
                        if ((First.Position - player).sqrMagnitude > 6f * 6f || now >= StepEnd)
                        {
                            // Nobody standing over them any more: they take their chance.
                            _heldByPlayer = false;
                            First.Quiet();
                            First.Go(_outside, Pace.Run, _outside, 20f);
                            Go(Leaving, now, 20f);
                        }
                        break;
                    }
                    if (First.Settled || now >= StepEnd)
                    {
                        // Out of the door. The loss stands as the game booked it.
                        if (_noticed)
                        {
                            ConcludeAmount("escaped", _amount, NotificationType.Error);
                        }
                        End();
                    }
                    else if (_guardHeld)
                    {
                        ChaseWithGuard(now);
                    }
                    break;

                case Stopped:
                    // Sent on their way, lighter than they hoped: gone at the door.
                    if (!First.Present || now >= StepEnd)
                    {
                        if (First.Present && !Within(First, _outside, 2.5f))
                        {
                            First.Quiet();
                            First.Go(_outside, Pace.Walk, _outside, 30f);
                            Go(Stopped, now, 31f);
                            _outAsked = true;
                        }
                        else
                        {
                            End();
                        }
                    }
                    else if (First.Settled && (_outAsked || Within(First, _outside, 2.5f)))
                    {
                        End();
                    }
                    break;
            }
        }

        private static bool Within(ActorRole who, Vector3 point, float metres)
        {
            return (who.Position - point).sqrMagnitude < metres * metres;
        }

        // The goods are back: the amount comes off the shop's stolen value, if these are still the same day's books.
        private void Recover(string ending)
        {
            FreeGuard();
            if (_amount > 0f && _shop != null && TimeHelper.CurrentDay == _day)
            {
                _shop.stolenItemsCost = Mathf.Max(0f, _shop.stolenItemsCost - _amount);
            }
            ConcludeAmount(ending, _amount, NotificationType.Success);
            _amount = 0f;
            _noticed = true;
        }

        // ---------------------------------------------------------------- the shop's own guard

        // His patrol is switched off and he is sent after the thief; FreeGuard switches it back on.
        private void HoldGuard()
        {
            try
            {
                if (_guard != null && !_guard.IsAway && _guard.enabled)
                {
                    _guard.enabled = false;
                    _guardHeld = true;
                    _noticed = true;
                    Crime.Show(NotificationType.Info, "reallife_crime_shoptheft_guard", 6f);
                }
            }
            catch (Exception e)
            {
                Crime.Fault(e);
                FreeGuard();
            }
        }

        private void ChaseWithGuard(float now)
        {
            if (_guard == null || _guard.employeeTpc == null)
            {
                _guardHeld = false;   // destroyed with the shop floor
                _guard = null;
                return;
            }
            NavMeshAgent agent = _guard.employeeTpc.navmeshAgent;
            if (agent == null || !agent.enabled || !agent.isOnNavMesh)
            {
                FreeGuard();
                return;
            }
            Vector3 thief = First.Position;
            if ((_guard.employeeTpc.transform.position - thief).sqrMagnitude < 2.2f * 2.2f)
            {
                Vector3 guardAt = _guard.employeeTpc.transform.position;   // (Recover lets the guard go)
                Recover("guard");
                First.Stand(guardAt);
                First.Surrender();
                Go(Stopped, now, 5f);
            }
            else if (now >= NextRepath)
            {
                NextRepath = now + RepathInterval;
                agent.isStopped = false;
                agent.SetDestination(thief);
            }
        }

        private void FreeGuard()
        {
            SecurityGuardEmployee guard = _guard;
            bool held = _guardHeld;
            _guardHeld = false;
            _guard = null;
            if (!held || guard == null)
            {
                return;
            }
            try
            {
                guard.enabled = true;   // his own patrol picks up from wherever he is standing
            }
            catch (Exception e)
            {
                Crime.Fault(e);
            }
        }

        protected override void Release()
        {
            FreeGuard();
            _shop = null;
        }
    }
}
