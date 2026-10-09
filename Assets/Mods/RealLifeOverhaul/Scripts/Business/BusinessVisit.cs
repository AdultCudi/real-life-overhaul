using BigAmbitions.Characters;
using Helpers;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// Somebody who comes into the player's own shop about an issue while the player is standing in
    /// it: the racket's collector, an inspector with a clipboard, and the collector once more with
    /// the police a step behind him. Played by the street scenes' own people (ActorPool, through
    /// ActorRole), who come in at the shop's real door, walk on the shop's navigation mesh, stand
    /// only where the mesh says a person can, and leave by the door. No customer, employee, shelf
    /// or sale is touched. The arrest is Crime/ArrestSequence, the one every scene uses.
    ///
    /// One at a time, ticked four times a second by the director only while it is on, never longer
    /// than <see cref="MostSeconds"/>, and ended at once (everybody back in the pool) when the
    /// player leaves the shop, a crime scene starts, or anything unexpected happens. If nobody can
    /// be had from the pool, there is no visit and the issue goes on by phone as it would have.
    /// </summary>
    internal sealed class BusinessVisit
    {
        public enum Kind { Collector, Inspector, Sting }

        private enum Step { None, Arriving, Looking, Looking2, ToPlayer, Talking, Waiting, Police, Arrest, Leaving }

        private const float MostSeconds = 110f;
        private const float TalkDistance = 1.7f;

        // What the collector (and the man of a sting) does with his hands while he speaks, and while he waits for the answer.
        // NOT collector_lean: that clip leans on a counter 0.975 m up whose near edge is 0.32 to 0.36 m ahead, with whoever
        // he speaks to 1.1 m beyond it, and reads as a man bent forward with a hand in the air anywhere else
        // (CityAssets/README.md). A visit is played where the player happens to stand, 1.7 m from them on open floor, and the
        // game's shop furniture says nothing of a counter top's height or edge: no such place can be found, so the clips
        // that read standing free are used. Should a shop ever offer a measured counter, it is one line here.
        private const string TalkClip = "talk_a";          // talking with the hands, to somebody in front
        private const string WaitClip = "arms_crossed";    // standing square with folded arms: waiting to be paid

        private static readonly string[] CollectorLines = { "reallife_biz_visit_collector_1", "reallife_biz_visit_collector_2", "reallife_biz_visit_collector_3" };
        private static readonly string[] InspectorLines = { "reallife_biz_visit_inspector_1", "reallife_biz_visit_inspector_2", "reallife_biz_visit_inspector_3" };
        private static readonly string[] StingLines = { "reallife_biz_visit_sting_1", "reallife_biz_visit_sting_2" };

        private readonly ActorRole _visitor = new ActorRole();
        private readonly ActorRole _officer = new ActorRole();
        private readonly ArrestSequence _arrest = new ArrestSequence();
        private Kind _kind;
        private Step _step;
        private float _stepEnd;
        private float _deadline;
        private BuildingManager _building;
        private BusinessIssue _issue;
        private Vector3 _door;
        private Vector3 _outside;
        private int _line = -1;
        private bool _opened;
        private bool _told;          // a sting: the arrest has been announced

        public bool Active => _step != Step.None;

        /// <summary>The issue the visitor has come about, once they are standing with the player and want an answer; null otherwise.</summary>
        public BusinessIssue Asking => _step == Step.Waiting ? _issue : null;

        /// <summary>
        /// Starts a visit in the shop the player is standing in. False (with nothing on the floor) if it
        /// cannot be played here and now.
        /// </summary>
        public bool Begin(Kind kind, BusinessIssue issue, float now)
        {
            End();
            if (!BuildingManager.IsInsideBuilding || ActorPool.Instance == null || CrimeDirector.IsActive || !Settings.People
                || (kind == Kind.Sting && CrimeDirector.Instance == null))
            {
                return false;
            }
            PlayerController player = PlayerHelper.PlayerController;
            if (player == null || !ShopFloor.Door(player.transform.position, out _door, out _outside)
                || !_visitor.Enter(_door, ActorLook.Civilian, 2f, onRoad: false))
            {
                return false;
            }
            _building = InstanceBehavior<BuildingManager>.Instance;
            _kind = kind;
            _issue = issue;
            _opened = false;
            _told = false;
            _deadline = now + MostSeconds;
            if (kind == Kind.Inspector && ShopFloor.RandomSpot(out Vector3 floor))
            {
                _visitor.Go(floor, Pace.Walk, floor + Crime.RandomHeading(), 25f);
                _visitor.Work(ClipLibrary.Has("inspect_look") ? "inspect_look" : "clipboard_write");   // worn whenever they stand: looking the place over, writing it down
                Go(Step.Arriving, now, 26f);
            }
            else
            {
                Approach(player.transform.position, now);
            }
            BusinessGame.Logger?.Info("Business: a visit begins in the shop (" + kind + ").");
            return true;
        }

        private void Approach(Vector3 player, float now)
        {
            _visitor.Go(Crime.Short(player, _visitor.Position, TalkDistance), Pace.Walk, player, 25f);
            Go(Step.ToPlayer, now, 26f);
        }

        private void Go(Step step, float now, float seconds)
        {
            _step = step;
            _stepEnd = now + seconds;
        }

        public void Tick(float now)
        {
            if (_step == Step.None)
            {
                return;
            }
            PlayerController controller = PlayerHelper.PlayerController;
            if (controller == null || !BuildingManager.IsInsideBuilding || InstanceBehavior<BuildingManager>.Instance != _building
                || now >= _deadline || CrimeDirector.IsActive)
            {
                End();   // the player has left the shop (or something else has the floor): nobody is left standing in it
                return;
            }
            Vector3 player = controller.transform.position;
            _visitor.Tick(now);
            _officer.Tick(now);
            if (!_visitor.Present && _step != Step.Arrest)
            {
                End();
                return;
            }
            switch (_step)
            {
                case Step.Arriving:
                    if (_visitor.Settled || now >= _stepEnd)
                    {
                        Go(Step.Looking, now, Random.Range(7f, 10f));
                    }
                    break;

                case Step.Looking:
                    if (now >= _stepEnd)
                    {
                        if (ShopFloor.RandomSpot(out Vector3 second))
                        {
                            _visitor.Go(second, Pace.Walk, second + Crime.RandomHeading(), 20f);
                            Go(Step.Looking2, now, 28f);
                        }
                        else
                        {
                            Approach(player, now);
                        }
                    }
                    break;

                case Step.Looking2:
                    if ((_visitor.Settled && now >= _stepEnd - 20f) || now >= _stepEnd)
                    {
                        Approach(player, now);
                    }
                    break;

                case Step.ToPlayer:
                    if (_visitor.Settled || now >= _stepEnd)
                    {
                        _visitor.Stand(player);
                        Notifications.Show(_kind == Kind.Inspector ? NotificationType.Info : NotificationType.Warning,
                            JobText.Pick(_kind == Kind.Collector ? CollectorLines : _kind == Kind.Inspector ? InspectorLines : StingLines, ref _line), null, 8f);
                        // They are talking to somebody who is standing in front of them: the gesture has someone to be made at.
                        bool close = (player - _visitor.Position).sqrMagnitude < 9f;
                        if (close && _kind != Kind.Inspector)
                        {
                            if (ClipLibrary.Has(TalkClip))
                            {
                                _visitor.Work(TalkClip);
                            }
                            else
                            {
                                _visitor.Repeat(AnimationType.IDontThinkSo, 2.2f, 3.6f);
                            }
                        }
                        Go(_kind == Kind.Sting ? Step.Police : _kind == Kind.Collector && _issue != null ? Step.Waiting : Step.Talking, now,
                            _kind == Kind.Sting ? 3.5f : _kind == Kind.Collector ? 14f : 6f);
                    }
                    break;

                case Step.Waiting:
                    // The director opens the desk on this issue as soon as no screen is in the way; then he waits for the answer.
                    if (now >= _stepEnd || _opened)
                    {
                        Go(Step.Talking, now, _opened ? 30f : 1f);
                    }
                    break;

                case Step.Talking:
                    if (now >= _stepEnd || (_opened && !BusinessDesk.DialogOpen))
                    {
                        Leave(now);
                    }
                    break;

                case Step.Police:
                    if (now >= _stepEnd)
                    {
                        _visitor.Quiet();
                        if (_officer.Enter(_door, ActorLook.Officer, 2f, onRoad: false))
                        {
                            _officer.Stand(_visitor.Position);
                            Notifications.Show(NotificationType.Success, "reallife_biz_visit_police", null, 8f);
                            ShopFloor.StartleCustomers(CharacterEmojiName.CommonYellowNeutral, 3);
                            _arrest.BeginIndoors(_visitor, _officer, _outside, now);
                            Go(Step.Arrest, now, 70f);
                        }
                        else
                        {
                            // Nobody to play the officer: he is taken at the door instead, and that is said.
                            _told = true;
                            Notifications.Show(NotificationType.Success, "reallife_biz_visit_police_outside", null, 9f);
                            Leave(now);
                        }
                    }
                    break;

                case Step.Arrest:
                    _arrest.Tick(now, player);
                    if (_arrest.Finished || !_arrest.Active || now >= _stepEnd)
                    {
                        _told = true;
                        Notifications.Show(NotificationType.Success, "reallife_biz_racket_ended", null, 12f);
                        End();
                    }
                    break;

                case Step.Leaving:
                    if (!_visitor.Present || now >= _stepEnd)
                    {
                        End();
                    }
                    break;
            }
        }

        /// <summary>The desk has been opened on the visitor's issue: he waits while the player answers.</summary>
        public void Opened()
        {
            if (!_opened && _step != Step.None && _kind == Kind.Collector && _visitor.Settled && ClipLibrary.Has(WaitClip))
            {
                _visitor.Work(WaitClip);   // he has said his piece: arms folded until he has his answer
            }
            _opened = true;
        }

        private void Leave(float now)
        {
            _visitor.Quiet();
            _visitor.WalkOff(_outside, Pace.Walk, 22f);
            Go(Step.Leaving, now, 23f);
        }

        /// <summary>Every way out: cuffs off, everybody back in the pool. Safe at any time, any number of times.</summary>
        public void End()
        {
            bool was = _step != Step.None;
            if (was && _kind == Kind.Sting && !_told)
            {
                // Cut short (the player left, the floor was needed): he is taken all the same, and that is said.
                _told = true;
                Notifications.Show(NotificationType.Success, "reallife_biz_visit_police_outside", null, 10f);
            }
            _step = Step.None;
            _issue = null;
            _building = null;
            try
            {
                _arrest.Clear();
                _visitor.Exit();
                _officer.Exit();
            }
            catch (System.Exception e)
            {
                BusinessGame.Logger?.Error(e);
            }
            if (was)
            {
                BusinessGame.Logger?.Info("Business: the visit is over; nobody of the mod's is left in the shop.");
            }
        }
    }
}
