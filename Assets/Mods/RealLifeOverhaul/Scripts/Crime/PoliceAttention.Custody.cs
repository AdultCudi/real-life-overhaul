using System;
using System.Collections;
using System.Collections.Generic;
using BigAmbitions.DayNightCycle;
using Helpers;
using UI;
using UI.Notification;
using UnityEngine;
using Random = UnityEngine.Random;

namespace RealLifeOverhaul
{
    /// <summary>Custody when no precinct holds the player: the screen goes dark and the clock moves on.</summary>
    public partial class PoliceAttention
    {
        private bool _custodyRunning;
        private bool _custodyFaded;
        private bool _custodyResting;

        private void OnHitAndRunCaught()
        {
            if (_state == State.Idle && !_custodyRunning && isActiveAndEnabled)
            {
                BeginCustody(Random.Range(CrimeLaw.CustodyHoursMin, CrimeLaw.CustodyHoursMax + 1));
            }
        }

        private void BeginCustody(int hours)
        {
            if (_custodyRunning)
            {
                return;
            }
            _state = State.Custody;
            _custodyRunning = true;
            News.Report(NewsKind.PlayerArrested, PlayerHelper.GetPosition());
            SocialHooks.PlayerArrested();
            Wiring.Arrests.FamilyHears(0);   // told at home, as when the Law feature does not hold the player
            StartCoroutine(Guard.Routine(CustodyRoutine(hours)));
        }

        // The screen goes dark, the clock moves on by the game's own time skip (as its doctor and taxi do), and the player is
        // let go where they were. The finally puts the game back whatever happens.
        private IEnumerator CustodyRoutine(int hours)
        {
            _custodyFaded = false;
            _custodyResting = false;
            try
            {
                yield return new WaitForSecondsRealtime(5f);

                // Never over a dialog, a menu, another time skip or a hospital trip: then it is the fine alone.
                float waited = 0f;
                while (!LifeEvents.IsSafeForEvent() && waited < 15f)
                {
                    waited += Time.unscaledDeltaTime;
                    yield return null;
                }
                UIs uis = InstanceBehavior<UIs>.Instance;
                if (!LifeEvents.IsSafeForEvent() || uis == null || uis.timeMachine == null || SaveGameManager.Current == null)
                {
                    yield break;
                }

                _custodyFaded = true;
                yield return UiFader.Fade(0.7f, "reallife_crime_custody_fade");
                Clear();   // patrol car and officer go while nobody can see
                _state = State.Custody;

                Timestamp release = TimeHelper.Now();
                release.AddHours(hours);
                _custodyResting = true;
                EnergyHelper.SetCurrentEnergyRegen(EnergyRegen.Hospital);   // a bench in a cell: a little rest, as in the game's waiting rooms
                if (!GoingPublic.ClockShared)
                {
                    uis.timeMachine.StartTimeMachine(release, disableCancel: true, "reallife_crime_custody_info");   // the clock is not skipped in Going Public
                }

                waited = 0f;
                while (uis != null && uis.timeMachine != null && uis.timeMachine.isRunning && waited < 240f)
                {
                    waited += Time.unscaledDeltaTime;
                    yield return null;
                }
                _custodyResting = false;
                EnergyHelper.SetCurrentEnergyRegen(EnergyRegen.None);
                _custodyFaded = false;
                yield return UiFader.UnFade();
                if (SaveGameManager.Current != null)
                {
                    ModState.AddModifier(ModState.MoodDownBig);
                    var data = new Dictionary<string, string>(1) { { "hours", hours.ToString() } };
                    Notifications.Show(NotificationType.Info, "reallife_crime_custody_released", data, 10f);
                }
            }
            finally
            {
                EndCustody();
            }
        }

        // Puts back whatever the custody routine had changed. Safe more than once.
        private void EndCustody()
        {
            try
            {
                if (_custodyResting)
                {
                    _custodyResting = false;
                    EnergyHelper.SetCurrentEnergyRegen(EnergyRegen.None);
                }
                if (_custodyFaded)
                {
                    _custodyFaded = false;
                    if (isActiveAndEnabled)
                    {
                        StartCoroutine(UiFader.UnFade());
                    }
                }
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
            _custodyRunning = false;
            Clear();
        }
    }
}
