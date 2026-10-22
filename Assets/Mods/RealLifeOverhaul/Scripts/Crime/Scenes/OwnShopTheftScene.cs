using System;
using Localizor;
using UI.Notification;
using UnityEngine;
using UnityEngine.AI;
using Random = UnityEngine.Random;

namespace RealLifeOverhaul
{
    /// <summary>
    /// A theft the game has just booked in the player's own shop, acted out by one of the mod's people while the player is
    /// inside; the game alone takes the stock and books the loss. A camera on the shelf or a close look tells the player,
    /// and a guard may give chase. Confronted, the thief drops it, pays or bolts; a 911 call puts an officer at the door.
    /// A thief stopped takes the amount off the shop's stolen value again, within the same day's books.
    /// </summary>
    internal sealed class OwnShopTheftScene : CrimeScene
    {
        private const int Concealing = 0, Leaving = 1, Stopped = 2;
        private const float ConfrontReach = 3f;   // metres

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
        public override bool Interactive => Step <= Leaving && !_confronted;   // told or not: sharp eyes count
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
            _day = setup.Forced ? -1 : TimeHelper.CurrentDay;   // a test booked nothing, so takes nothing off the books
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
            _confronted = true;   // once: what the thief does is settled now
            _noticed = true;
            FreeGuard();
            First.Quiet();
            float roll = Random.value;
            if (_copWanted || Cop.Present)
            {
                First.Stand(player);   // the police are coming and the player stands over them
                First.Surrender();
                Crime.Show(NotificationType.Success, "reallife_crime_shoptheft_held", 7f);
                _heldByPlayer = true;
                Go(Leaving, now, 40f);
            }
            else if (roll < 0.45f)
            {
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
                            _heldByPlayer = false;   // nobody stands over them now: they take their chance
                            First.Quiet();
                            First.Go(_outside, Pace.Run, _outside, 20f);
                            Go(Leaving, now, 20f);
                        }
                        break;
                    }
                    if (First.Settled || now >= StepEnd)
                    {
                        if (_noticed)   // out of the door: the loss stands as the game booked it
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
                    if (!First.Present || now >= StepEnd)   // sent on their way: gone at the door
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

        // The goods are back: off the shop's stolen value again, while these are still the same day's books.
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

        // The guard's own patrol is switched off while the mod sends them after the thief; FreeGuard turns it back on.
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
                Vector3 guardAt = _guard.employeeTpc.transform.position;   // read first: Recover lets the guard go
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
                guard.enabled = true;   // the guard's own patrol picks up from where they stand
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
