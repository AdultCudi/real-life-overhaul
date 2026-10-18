using System;
using System.Collections;
using System.IO;
using Helpers;
using UI;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul.Family
{
    /// <summary>FamilyDirector's part for what takes time: sequences under a fade, the photograph, and the staged scenes.</summary>
    public sealed partial class FamilyDirector
    {
        private const string ShotsFolder = "RealLifeOverhaul_shots";

        /// <summary>Runs a sequence (a fade, a skip of time, what follows). One at a time; a second is refused.</summary>
        internal bool Run(IEnumerator routine)
        {
            if (Busy || !isActiveAndEnabled)
            {
                return false;
            }
            StartCoroutine(Guard.Routine(Wrap(routine)));
            return true;
        }

        private IEnumerator Wrap(IEnumerator routine)
        {
            Busy = true;
            while (true)
            {
                bool more;
                try
                {
                    more = routine.MoveNext();
                }
                catch (Exception e)
                {
                    Logger?.Error(e);
                    Recover();
                    break;
                }
                if (!more)
                {
                    break;
                }
                yield return routine.Current;
            }
            Busy = false;
        }

        // A sequence faulted or was cut off part of the way: the screen is given back and nothing stays held.
        private void Recover()
        {
            try
            {
                EnergyHelper.SetCurrentEnergyRegen(EnergyRegen.None);
                Body.Release();
                if (UiFader.isFading && isActiveAndEnabled)
                {
                    StartCoroutine(UiFader.UnFade());
                }
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        /// <summary>
        /// The shape of something that takes time: the screen goes dark with a line on it, <paramref name="during"/> happens
        /// unseen, so many hours pass on the game's own time skip, the screen comes back and <paramref name="after"/> follows.
        /// </summary>
        internal IEnumerator Passage(string infoKey, int hours, Action during, Action after)
        {
            float waited = 0f;
            while ((DialogController.current != null || HudConfirm.isOpen) && waited < 20f)
            {
                waited += Time.unscaledDeltaTime;
                yield return null;
            }
            yield return UiFader.Fade(0.5f, infoKey);
            during?.Invoke();
            UIs uis = InstanceBehavior<UIs>.Instance;
            if (hours > 0 && !GoingPublic.ClockShared && uis != null && uis.timeMachine != null && !uis.timeMachine.isRunning)
            {
                // A day at a time: longer than that and hunger alone would put somebody in hospital.
                int left = hours;
                while (left > 0)
                {
                    int chunk = Mathf.Min(left, 24);
                    left -= chunk;
                    if (hours >= 24)
                    {
                        SaveGameManager.Current.Energy = 100f;
                        SaveGameManager.Current.Hunger = 100f;
                    }
                    if (hours >= 4)
                    {
                        EnergyHelper.SetCurrentEnergyRegen(hours >= 24 ? EnergyRegen.Bed : EnergyRegen.Bench);   // nobody collapses half-way through their own wedding
                    }
                    uis.timeMachine.StartTimeMachine(TimeHelper.GetTimeInHours(chunk), true, infoKey);
                    while (uis.timeMachine.isRunning)
                    {
                        yield return null;
                    }
                    EnergyHelper.SetCurrentEnergyRegen(EnergyRegen.None);
                    yield return null;
                }
                if (hours >= 24)
                {
                    SaveGameManager.Current.Energy = 100f;
                    SaveGameManager.Current.Hunger = 100f;
                }
            }
            yield return UiFader.UnFade();
            after?.Invoke();
        }

        /// <summary>Does something once the player is free again: the conversation closed, no menu up.</summary>
        internal void WhenFree(Action action)
        {
            if (isActiveAndEnabled)
            {
                StartCoroutine(Guard.Routine(Free(action)));
            }
        }

        private IEnumerator Free(Action action)
        {
            float waited = 0f;
            yield return null;
            while ((DialogController.current != null || HudConfirm.isOpen || Busy) && waited < 60f)
            {
                waited += Time.unscaledDeltaTime;
                yield return null;
            }
            Guarded(action);
        }

        /// <summary>A photograph: the game's HUD is hidden for two frames and the screen written to the mod's folder in the game's data folder.</summary>
        internal IEnumerator Photograph(string name)
        {
            UIs uis = InstanceBehavior<UIs>.Instance;
            bool hud = false, phone = false, top = false;
            string file = null;
            try
            {
                string folder = Path.Combine(Application.persistentDataPath, ShotsFolder);
                Directory.CreateDirectory(folder);
                file = Path.Combine(folder, name + "_day" + TimeHelper.CurrentDay + "_" + DateTime.Now.ToString("HHmmss") + ".png");
                if (uis != null)
                {
                    hud = uis.playerHUD != null && uis.playerHUD.gameObject.activeSelf;
                    phone = uis.smartphoneUI != null && uis.smartphoneUI.gameObject.activeSelf;
                    top = uis.topBar != null && uis.topBar.gameObject.activeSelf;
                    Show(uis, hud, phone, top, false);
                }
            }
            catch (Exception e)
            {
                Logger?.Error(e);
                file = null;
            }
            yield return null;
            if (file != null)
            {
                ScreenCapture.CaptureScreenshot(file);
            }
            yield return null;
            yield return null;
            try
            {
                if (uis != null)
                {
                    Show(uis, hud, phone, top, true);
                }
                if (file != null)
                {
                    Logger?.Info("Family: photograph written to " + file);
                    FamilyText.Toast(NotificationType.Success, "reallife_family_photo_saved", new Args { Text = ShotsFolder });
                }
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        // The HUD, the phone and the top bar, each only if it was showing.
        private static void Show(UIs uis, bool hud, bool phone, bool top, bool shown)
        {
            if (hud)
            {
                uis.playerHUD.gameObject.SetActive(shown);
            }
            if (phone)
            {
                uis.smartphoneUI.gameObject.SetActive(shown);
            }
            if (top)
            {
                uis.topBar.gameObject.SetActive(shown);
            }
        }

        /// <summary>Why nothing with people in it can be staged right now; None if it can.</summary>
        internal TestRefusal SceneRefusal(bool outdoors)
        {
            if (!ModState.Loaded || !FamilyState.Loaded || SaveGameManager.Current == null || PlayerHelper.PlayerController == null)
            {
                return TestRefusal.NotLoaded;
            }
            if (_failed || !Settings.Family)
            {
                return TestRefusal.FeatureOff;
            }
            if (Scene != null || Busy || JobDirector.OnShift)
            {
                return TestRefusal.SceneActive;
            }
            if (PoliceAttention.IsBusy)
            {
                return TestRefusal.PoliceBusy;
            }
            if (PlayerHelper.IsUsingVehicle)
            {
                return TestRefusal.NothingFits;
            }
            if (outdoors && BuildingManager.IsInsideBuilding)
            {
                return TestRefusal.Indoors;
            }
            return TestRefusal.None;
        }

        internal bool StartScene(FamilyScene scene, out string refusal)
        {
            refusal = null;
            if (Scene != null || Busy)
            {
                refusal = "reallife_family_no_busy";
                return false;
            }
            try
            {
                if (!scene.Begin(this, out refusal))
                {
                    scene.End();
                    Logger?.Info("Family: " + scene.Name + " not started (" + (refusal ?? "no reason given") + ").");
                    return false;
                }
            }
            catch (Exception e)
            {
                Logger?.Error(e);
                try { scene.End(); } catch (Exception inner) { Logger?.Error(inner); }
                refusal = "reallife_family_no_fault";
                return false;
            }
            Scene = scene;
            _nextTick = 0f;
            Logger?.Info("Family: " + scene.Name + " started.");
            return true;
        }

        internal void EndScene()
        {
            FamilyScene scene = Scene;
            Scene = null;
            if (scene == null)
            {
                return;
            }
            try
            {
                scene.End();
            }
            catch (Exception e)
            {
                Logger?.Error(e);
                Cast.ReleaseAll();
                Props.ReturnAll();
                Body.Release();
            }
            Logger?.Info("Family: " + scene.Name + " ended.");
        }
    }
}
