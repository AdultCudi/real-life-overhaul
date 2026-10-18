using System;
using System.Collections;
using GleyTrafficSystem;
using Helpers;
using UI;
using UnityEngine;

namespace RealLifeOverhaul.Law
{
    /// <summary>The arrest where it happened: out of the car, an officer behind the player, hands up, cuffs.</summary>
    public sealed partial class Custody
    {
        private const NavigationBlocker Blocker = NavigationBlocker.DoctorOperation;   // any blocker stops the player; nothing else on a street sets this one
        private const string CuffingClip = SceneClips.ArrestCuffed;     // where it cannot be had the cuffs simply go on
        private const string OfficerCuffClip = SceneClips.OfficerCuff;  // the officer's half: whole body, from 0.9 m straight behind

        private readonly ActorRole _officer = new ActorRole();
        private PlayerController _player;
        private bool _held;                  // kept on the spot
        private bool _cuffed;
        private bool _clip;                  // one of the mod's clips is on the player

        private IEnumerator Scene(VehicleComponent patrol)
        {
            var gm = InstanceBehavior<GameManager>.Instance;
            _player = PlayerHelper.PlayerController;
            if (gm == null || _player == null)
            {
                yield break;
            }

            // The car is remembered so that it can be brought to the precinct: their own only, never another player's.
            VehicleInstance car = PlayerHelper.IsUsingVehicle ? VehicleTow.LeftBehind(gm.selectedVehicle) : null;
            if (car != null)
            {
                LawState.Custody.VehicleId = car.id;
            }
            yield return LeaveVehicle(4f, force: false);
            if (PlayerHelper.IsUsingVehicle || BuildingManager.IsInsideBuilding || !Settings.People || _player.Character == null
                || RagdollSystem.Holds(_player.Character))
            {
                yield break;   // still rolling, indoors or knocked down: the fade follows
            }

            HoldPlayer();
            Vector3 at = _player.transform.position;
            Vector3 forward = _player.transform.forward;
            forward.y = 0f;
            forward = forward.sqrMagnitude < 0.01f ? Vector3.forward : forward.normalized;
            Vector3 right = Vector3.Cross(Vector3.up, forward);

            // The officer comes from a door of the patrol car, or pavement out of view.
            Vector3 from = default;
            bool fromCar = false;
            if (patrol != null)
            {
                fromCar = StreetSpots.BesideCar(patrol, StreetSpots.Left, 1f, out from) || StreetSpots.BesideCar(patrol, StreetSpots.Right, 1f, out from);
            }
            bool origin = fromCar || StreetSpots.PavementAround(at, 9f, 17f, true, out from);

            // ...and ends up a pace behind them, or to a side; never in a vehicle, never on top of them.
            Vector3 stand = default;
            bool room = false;
            for (int i = 0; i < 4 && !room; i++)
            {
                Vector3 direction = i == 0 ? -forward : i == 1 ? -right : i == 2 ? right : forward;
                room = StreetSpots.OnRoad(at + direction * 0.9f, 0.35f, out stand) && JobSpots.Flat(stand, at) > 0.65f && JobSpots.Clear(stand, null);
            }
            if (!origin || !room || !_officer.Enter(from, ActorLook.Officer, 2f, onRoad: true))
            {
                Logger?.Info($"Law: no officer could walk up (origin {origin}, room {room}); taken in without the scene.");
                yield return Wait(2f);
                yield break;
            }
            if (fromCar)
            {
                ActorPool.PlayDoorSound(from);
            }
            // To that place itself: the officer's clip is measured from 0.9 m off.
            _officer.GoExactly(stand, JobSpots.Flat(from, at) > 14f ? Pace.Jog : Pace.Walk, at, 16f);

            // Hands up while the officer comes: raised in one clip, kept up in another.
            float raisedAt = Play(SceneClips.HandsUp, ClipEnd.Hold) && ClipLibrary.TryGet(SceneClips.HandsUp, out CityClip raise)
                ? Time.unscaledTime + Mathf.Max(0.3f, raise.Length - 0.1f) : -1f;
            float until = Time.unscaledTime + 17f;
            float nextTick = 0f;
            while (Time.unscaledTime < until && _officer.Present && !_officer.Settled)
            {
                _beat = Time.unscaledTime;
                if (Time.time >= nextTick)
                {
                    nextTick = Time.time + 0.25f;
                    _officer.Tick(Time.time);
                    if (raisedAt > 0f && Time.unscaledTime >= raisedAt)
                    {
                        raisedAt = -1f;
                        Play(SceneClips.HandsUpHold, ClipEnd.Return);
                    }
                }
                if (Interrupted())
                {
                    yield break;
                }
                yield return null;
            }
            if (!_officer.Present)
            {
                yield break;   // taken back by the pool: straight to the fade
            }

            // Cuffed: the arms are the clip's where it can be had, CuffPose's otherwise; the cuffs on the wrists are CuffPose's.
            if (JobSpots.Flat(_officer.Position, at) < 2.2f && !Interrupted())
            {
                _officer.Stand(at);
                // The two clips are made for the officer 0.9 m straight behind, both facing the same way, started together:
                // the player is turned with their back to the officer, and the officer's half plays only from that distance.
                if (ClipLibrary.Has(CuffingClip) && ClipLibrary.TryGet(CuffingClip, out CityClip cuffing))
                {
                    Vector3 back = _officer.Position - at;
                    back.y = 0f;
                    float gap = back.magnitude;
                    bool pair = gap > 0.3f && ClipLibrary.Has(OfficerCuffClip);
                    if (pair)
                    {
                        back /= gap;
                        yield return TurnPlayer(-back);
                        pair = !Interrupted() && _officer.Present && Vector3.Dot(_player.transform.forward, -back) >= 0.97f
                               && Mathf.Abs(gap - SceneClips.CuffBehind) <= SceneClips.CuffSlack;
                    }
                    if (Interrupted() || !_officer.Present)
                    {
                        yield break;
                    }
                    if (Play(CuffingClip, ClipEnd.Hold))
                    {
                        // Both in this frame; the officer's ends stepped in with a hand on the shoulder, held.
                        bool officerPlays = pair && _officer.PlaceAt(at + back * SceneClips.CuffBehind, at) && _officer.Act(OfficerCuffClip, ClipEnd.Hold);
                        if (!officerPlays)
                        {
                            _officer.Gesture(CrimeSceneGestures.Reach);
                        }
                        Logger?.Info(FormattableString.Invariant($"Law: cuffed in the clip, the officer {gap:F2} m behind (the pair is made for {SceneClips.CuffBehind:F2}): ")
                                     + (officerPlays ? "the officer's half is played." : "the officer reaches instead."));
                        float click = Mathf.Clamp(cuffing.Mark(SceneClips.CuffClick, cuffing.Length * 0.6f), 0.1f, Mathf.Max(0.1f, cuffing.Length));
                        yield return Wait(click);
                        // The click: the pair on the wrists shown, the one in the officer's hand put away.
                        _cuffed = !Interrupted() && CuffPose.Put(_player.Character);
                        _officer.StowTool();
                        yield return Wait(Mathf.Max(0.05f, cuffing.Length - click));
                    }
                    else
                    {
                        _cuffed = CuffPose.Put(_player.Character);
                    }
                }
                else
                {
                    _cuffed = CuffPose.Put(_player.Character);
                }
                if (Interrupted())
                {
                    yield break;
                }
                if (!Play(SceneClips.CuffedStand, ClipEnd.Return))
                {
                    StopClip(atOnce: false);
                }
                Logger?.Info("Law: cuffed" + (_cuffed ? string.Empty : " (no cuff pose to be had: the fade follows)") + ".");
                yield return Wait(2.6f);
            }
        }

        // Turned on the spot to face a way, over a moment; given up after a second if something else has the heading.
        private IEnumerator TurnPlayer(Vector3 forward)
        {
            forward.y = 0f;
            if (_player == null || forward.sqrMagnitude < 0.01f)
            {
                yield break;
            }
            Quaternion goal = Quaternion.LookRotation(forward.normalized, Vector3.up);
            float until = Time.unscaledTime + 1.2f;
            while (!Interrupted() && Time.unscaledTime < until && Quaternion.Angle(_player.transform.rotation, goal) > 1f)
            {
                _player.transform.rotation = Quaternion.RotateTowards(_player.transform.rotation, goal, 300f * Time.unscaledDeltaTime);
                _beat = Time.unscaledTime;
                yield return null;
            }
        }

        // Out of whatever they are driving once it has stopped. With force (only under the dark screen) also out of one that
        // would not stop: the game's own exit halts it, and the impound brings it to the precinct later.
        private IEnumerator LeaveVehicle(float patience, bool force)
        {
            var gm = InstanceBehavior<GameManager>.Instance;
            if (gm == null || gm.selectedVehicle == null)
            {
                yield break;
            }
            if (PlayerHelper.IsUsingVehicle)
            {
                float waited = 0f;
                while (gm.selectedVehicle != null && Mathf.Abs(gm.selectedVehicle.CurrentSpeed) > 1f && waited < patience)
                {
                    waited += Time.unscaledDeltaTime;
                    _beat = Time.unscaledTime;
                    yield return null;
                }
                if (gm.selectedVehicle == null || (!force && Mathf.Abs(gm.selectedVehicle.CurrentSpeed) > 1f))
                {
                    yield break;
                }
                gm.selectedVehicle.ExitVehicle();
                yield return new WaitForSecondsRealtime(0.5f);
                _beat = Time.unscaledTime;
            }

            // The game clears the selected vehicle only when its HUD panel closes (Life/LifeEvents).
            UIs hud = InstanceBehavior<UIs>.Instance;
            if (gm.selectedVehicle != null && !PlayerHelper.IsUsingVehicle && hud != null && hud.playerHUD != null && hud.playerHUD.itemPanelUI != null)
            {
                hud.playerHUD.itemPanelUI.Toggle(false);
            }
        }

        // Cuffs off, the clip off, the officer gone, the player free to walk. Safe when none of it was on.
        private void EndScene()
        {
            try
            {
                _officer.Exit();
                if (_cuffed)
                {
                    _cuffed = false;
                    if (_player != null && _player.Character != null)
                    {
                        CuffPose.Remove(_player.Character);
                    }
                }
                StopClip(atOnce: true);   // under the dark screen or on a fault: no fade, which a paused game would never finish
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
            Free();
        }

        private void HoldPlayer()
        {
            if (_held || _player == null)
            {
                return;
            }
            _held = true;
            _player.ResetNavigation();
            _player.SetNavigationBlocker(Blocker);
        }

        private void Free()
        {
            if (!_held)
            {
                return;
            }
            _held = false;
            try
            {
                if (_player != null)
                {
                    _player.UnsetNavigationBlocker(Blocker);
                }
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        private bool Play(string name, ClipEnd end)
        {
            if (!_held || _player == null || !ActivityAnimators.Working || !ClipLibrary.Ready || !ClipLibrary.TryGet(name, out CityClip clip))
            {
                return false;
            }
            ThirdPersonCharacter body = _player.Character;
            if (body == null || body.animator == null || !body.visible || RagdollSystem.Holds(body))
            {
                return false;
            }
            ActivityAnimator animator = ActivityAnimators.For(body.animator);
            if (animator == null || !animator.Usable || !animator.Play(clip.Clip, 0.25f, 1f, end))
            {
                return false;
            }
            _clip = true;
            return true;
        }

        private void StopClip(bool atOnce)
        {
            if (!_clip)
            {
                return;
            }
            _clip = false;
            if (_player == null || _player.Character == null || _player.Character.animator == null)
            {
                return;
            }
            if (atOnce)
            {
                ActivityAnimators.Forget(_player.Character.animator);   // back to the game's own animation this instant
                return;
            }
            ActivityAnimator animator = ActivityAnimators.For(_player.Character.animator);
            if (animator != null)
            {
                animator.Stop(0.15f);
            }
        }

        private bool Interrupted()
        {
            return _player == null || _player.Character == null || RagdollSystem.Holds(_player.Character) || PlayerHelper.IsUsingVehicle
                   || BuildingManager.IsInsideBuilding || SaveGameManager.Current == null;
        }
    }
}
