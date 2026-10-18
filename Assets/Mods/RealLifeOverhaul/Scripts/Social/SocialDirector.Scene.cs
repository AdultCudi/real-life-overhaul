using System;
using Helpers;
using UI;
using UI.Load;
using UnityEngine;
using UnityEngine.Events;

namespace RealLifeOverhaul
{
    /// <summary>The social director's part for the scene that is on: starting it, ticking it, holding it up, ending it.</summary>
    public sealed partial class SocialDirector
    {
        private SocialScene _selfieScene;   // the outing a selfie was last liked on: one like an outing
        private Rigidbody _carBody;
        private CarController _hookedCar;
        private UnityAction<Collision> _onCollision;
        private float _lastCrash = -100f;
        private float _lastInsideAt = -100f;
        private float _lastContextAt;
        private Vector3 _lastOnFoot;
        private SceneBreak _held;           // what is holding the scene up: it waits, and is not ticked
        private float _heldFrom;
        private TestRefusal _lastRefusal;
        private float _lastRefusalAt = -100f;

        private bool Begin(SocialScene scene)
        {
            bool begun;
            try
            {
                begun = scene.Start(this, Time.time);
            }
            catch (Exception e)
            {
                Logger?.Error(e);
                begun = false;
            }
            if (!begun)
            {
                // Once a reason: somebody waiting to come over is tried again every few seconds.
                if (scene.Refusal != _lastRefusal || Time.unscaledTime - _lastRefusalAt > 60f)
                {
                    _lastRefusal = scene.Refusal;
                    _lastRefusalAt = Time.unscaledTime;
                    Logger?.Info("Social: could not be started (" + scene.Refusal + (scene.RefusalDetail != null ? "; " + scene.RefusalDetail : string.Empty) + "): "
                                 + Described(scene) + ".");
                }
                SafeEnd(scene, SceneEnd.Interrupted);
                Cast.ReleaseAll();
                return false;
            }
            _scene = scene;
            _held = SceneBreak.None;
            _nextTick = 0f;
            _lastInsideAt = Time.time;
            return true;
        }

        private void Tick(float now)
        {
            var gm = InstanceBehavior<GameManager>.Instance;
            SceneBreak why = Broken(now, gm);
            if (why != SceneBreak.None && !CanWait(why))
            {
                Stop(why);
                return;
            }
            var premises = InstanceBehavior<BuildingManager>.Instance;
            if (premises != null && (premises.enteringBuilding || premises.exitingBuilding))
            {
                return;   // going through a door: nothing is asked of anyone until they are through
            }
            if (why != SceneBreak.None)
            {
                if (_held == SceneBreak.None)
                {
                    if (!_scene.Hold(why, now))
                    {
                        Stop(why);
                        return;
                    }
                    _held = why;
                    _heldFrom = now;
                    Unguide();
                    Logger?.Info("Social: held up (" + why + "), waiting: " + Described(_scene) + ".");
                }
                if (!_scene.Holding(why, now, now - _heldFrom))
                {
                    EndScene(SceneEnd.Left, SceneBreak.GaveUp);
                }
                else if (_scene.Over)
                {
                    EndScene(SceneEnd.Done);
                }
                return;
            }
            if (_held != SceneBreak.None)
            {
                SceneBreak was = _held;
                _held = SceneBreak.None;
                _lastInsideAt = now;   // the street takes a moment to start up again
                RefreshContext(gm, now);
                Logger?.Info("Social: back on the street after " + (now - _heldFrom).ToString("F0") + " s (" + was + "); the scene goes on.");
                _scene.Resume(was, now, now - _heldFrom);
            }
            RefreshContext(gm, now);
            _scene.Tick(now);
            if (_scene == null)
            {
                return;
            }
            Guide();
            if (Cast.AnyOut)
            {
                Cast.Cull();
            }
            if (_scene.Over)
            {
                EndScene(SceneEnd.Done);
            }
        }

        // The gravest first. The game's delivery jobs are not among them: they leave the player free on the street.
        private SceneBreak Broken(float now, GameManager gm)
        {
            UIs uis = InstanceBehavior<UIs>.Instance;
            GameInstance save = SaveGameManager.Current;
            if (uis == null || save == null || gm == null || gm.playerController == null)
            {
                return SceneBreak.NoGame;
            }
            if (LoadScene.isLoading)
            {
                return SceneBreak.Loading;
            }
            if (GameMembers.PlayerDead)
            {
                return SceneBreak.PlayerDead;
            }
            if (EnergyHelper.goingToHospital)
            {
                return SceneBreak.Hospital;
            }
            if (_scene.SkippingTime)
            {
                _lastInsideAt = now;   // the street takes a moment to start up after the time machine too
                return SceneBreak.None;
            }
            if (_scene is OutingScene && !Settings.SocialOutings)
            {
                return SceneBreak.OutingsOff;
            }
            if (JobDirector.OnShift)
            {
                return SceneBreak.JobShift;
            }
            if (uis.timeMachine != null && uis.timeMachine.isRunning)
            {
                _lastInsideAt = now;
                return SceneBreak.TimeSkip;
            }
            if (BuildingManager.IsInsideBuilding)
            {
                _lastInsideAt = now;
                return SceneBreak.Indoors;
            }
            if (!TrafficTools.Street && now - _lastInsideAt > 6f)
            {
                return SceneBreak.StreetNotReady;
            }
            return SceneBreak.None;
        }

        private static bool CanWait(SceneBreak why)
        {
            return why == SceneBreak.Indoors || why == SceneBreak.StreetNotReady || why == SceneBreak.TimeSkip;
        }

        private void Stop(SceneBreak why)
        {
            if (_scene == null)
            {
                return;
            }
            try
            {
                _scene.Cut(why);
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
            EndScene(SceneEnd.Interrupted, why);
        }

        private string Described(SocialScene scene)
        {
            try
            {
                return scene.Describe();
            }
            catch (Exception e)
            {
                Logger?.Error(e);
                return scene.GetType().Name;
            }
        }

        // Out of doors, with the street running and nothing else having the player.
        private static bool StreetReady()
        {
            UIs uis = InstanceBehavior<UIs>.Instance;
            GameInstance save = SaveGameManager.Current;
            return uis != null && save != null && !LoadScene.isLoading && !BuildingManager.IsInsideBuilding && TrafficTools.Street
                   && (uis.timeMachine == null || !uis.timeMachine.isRunning) && !JobDirector.OnShift && !GameMembers.PlayerDead && !EnergyHelper.goingToHospital;
        }

        private void RefreshContext(GameManager gm, float now)
        {
            if (gm == null || gm.playerController == null)
            {
                return;
            }
            PlayerController player = gm.playerController;
            Body.Bind(player);
            var car = gm.selectedVehicle as CarController;
            if (car != Car)
            {
                Car = car;
                _carBody = car != null ? car.GetComponent<Rigidbody>() : null;   // once per vehicle
                Hook(car);
            }
            OnFoot = gm.selectedVehicle == null && !PlayerHelper.IsUsingVehicle;
            CarVelocity = _carBody != null ? _carBody.velocity : Vector3.zero;
            CarSpeed = CarVelocity.magnitude;
            Vector3 at = player.transform.position;
            float dt = now - _lastContextAt;
            PlayerSpeed = dt > 0.01f && dt < 1f ? JobSpots.Flat(at, _lastOnFoot) / dt : 0f;
            _lastOnFoot = at;
            _lastContextAt = now;
            PlayerPosition = car != null ? car.transform.position : at;
            Vector3 forward = player.transform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude > 0.01f)
            {
                PlayerForward = forward.normalized;
            }
        }

        private void EndScene(SceneEnd why, SceneBreak what = SceneBreak.None)
        {
            SocialScene scene = _scene;
            if (scene == null)
            {
                return;
            }
            _scene = null;
            SceneBreak held = _held;
            _held = SceneBreak.None;
            if (why != SceneEnd.Done)
            {
                Logger?.Info("Social: the scene ended early (" + why + (what != SceneBreak.None ? ": " + what : string.Empty)
                             + (held != SceneBreak.None ? ", while held up by " + held : string.Empty) + "): " + Described(scene)
                             + "; plan " + (SocialState.Plan.Active ? "still on" : "none") + ".");
            }
            try
            {
                if (Talk != null && SocialPhone.DialogOpen)
                {
                    InstanceBehavior<UIs>.Instance.playerHUD.dialogUI.FinishDialog();
                }
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
            Talk = null;
            HideUi();
            SafeEnd(scene, why);
            Cast.ReleaseAll();   // whoever a scene forgot
            Body.Release();
            Unhook();
            Unguide();
            Car = null;
            _carBody = null;
            if (why == SceneEnd.Done && scene is OutingScene outing && outing.Greeted && SocialLinks.OutingDone != null)
            {
                try
                {
                    SocialLinks.OutingDone(outing.Who, outing.Kind);
                }
                catch (Exception e)
                {
                    Logger?.Error(e);
                }
            }
        }

        private static void SafeEnd(SocialScene scene, SceneEnd why)
        {
            try
            {
                scene.End(why);
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        internal string OnSelfie(Vector3 where)
        {
            try
            {
                if (_broken || !SocialState.Loaded || !Settings.Social || !(_scene is OutingScene outing) || ReferenceEquals(_selfieScene, _scene)
                    || !outing.Beside(where, SocialRules.SelfieWithin))
                {
                    return null;
                }
                _selfieScene = _scene;
                SocialPerson person = outing.Who;
                SocialLife.Change(person, SocialRules.SelfieFamiliarity, SocialRules.SelfieAffection, 0f);
                Logger?.Info("Social: " + person.Name + " was in a selfie with the player on an outing and liked it.");
                return SocialLife.First(person);
            }
            catch (Exception e)
            {
                Logger?.Error(e);
                return null;
            }
        }

        internal int Companion(Vector3 where)
        {
            return !_broken && SocialState.Loaded && Settings.Social && _scene is OutingScene outing && outing.Beside(where, SocialRules.SelfieWithin) ? outing.Who.Index : -1;
        }

        // Hooked when the player's car changes, let go when the scene ends.
        private void Hook(CarController car)
        {
            Unhook();
            if (car != null && car.vehicleController != null)
            {
                _onCollision = _onCollision ?? Guard.Listener<Collision>(OnCollision);   // through the guard: a fault never reaches the vehicle's own listeners
                car.vehicleController.onCollision.AddListener(_onCollision);
                _hookedCar = car;
            }
        }

        private void Unhook()
        {
            if (_hookedCar != null && _hookedCar.vehicleController != null)
            {
                _hookedCar.vehicleController.onCollision.RemoveListener(_onCollision);
            }
            _hookedCar = null;
        }

        private void OnCollision(Collision collision)
        {
            if (_scene == null || _broken || Time.time - _lastCrash < 2.5f || _hookedCar == null)
            {
                return;
            }
            try
            {
                CarContact contact = CarContacts.Judge(collision, _hookedCar.transform);   // the car hitting something, never the road or a kerb
                if (!contact.Counts(3f))
                {
                    return;
                }
                _lastCrash = Time.time;
                _scene.OnCrash(contact.Impact);
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }
    }
}
