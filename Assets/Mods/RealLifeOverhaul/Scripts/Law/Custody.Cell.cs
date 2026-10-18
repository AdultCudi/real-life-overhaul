using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using BigAmbitions.DayNightCycle;
using Extensions;
using Helpers;
using UI;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul.Law
{
    /// <summary>The cell's list while the game's time skip serves the hours, and the release.</summary>
    public sealed partial class Custody
    {
        private const float HeldMost = 420f;         // real seconds a stay may take on screen, whatever the clock says

        private bool _resting;
        private bool _skipping;              // the running time skip is ours
        private int _clicked = -1;

        private IEnumerator Cell()
        {
            CustodyState state = LawState.Custody;
            if (GoingPublic.ClockShared)
            {
                // In a Going Public session the clock is everyone's: the hours are served at once.
                Logger?.Info($"Law: {state.Hours} h of custody served at once: the clock is the Going Public session's.");
                yield break;
            }
            Precinct precinct = _precincts.At(state.Precinct);
            if (_panel == null)
            {
                _panel = new CustodyList(Logger);
            }
            int priors = Record.TimesHeld(LawBook.RepeatDays);
            string sheet = state.Court ? Order(state) : Sheet(state);
            bool listed = _panel.Open(precinct != null ? precinct.Name : "reallife_law_precinct_city".Tr(), sheet,
                Fill(state.Court ? "reallife_law_cell_order_term" : priors > 0 ? "reallife_law_cell_sentence_repeat" : "reallife_law_cell_sentence", "hours", state.Hours.ToString()),
                _onClick, _inCell);
            if (state.Court)
            {
                // A court's order: sit it out, and bail only if the court set one; no lawyer.
                _panel.Shape(state.Bail > 0 ? 2 : 1, "reallife_law_cell_order");
            }
            int fee = LawyerFee;
            _panel.SetOption(0, "reallife_law_cell_wait".Tr(), "reallife_law_cell_wait_detail".Tr(), true);
            _panel.SetOption(1, state.Bail > 0 ? "reallife_law_cell_bail_name".Tr() : "reallife_law_cell_no_bail".Tr(),
                state.Bail > 0 ? Fill("reallife_law_cell_bail_detail", "amount", ((float)state.Bail).ToCurrencyFormat()) : null, state.Bail > 0);
            _panel.SetOption(2, state.Lawyer ? "reallife_law_cell_lawyer_used".Tr() : "reallife_law_cell_lawyer_name".Tr(),
                state.Lawyer ? null : Fill("reallife_law_cell_lawyer_detail", "amount", ((float)fee).ToCurrencyFormat()), !state.Lawyer);
            _clicked = -1;
            ShowClock(state);
            if (listed)
            {
                _panel.Show();
            }

            _resting = true;
            EnergyHelper.SetCurrentEnergyRegen(EnergyRegen.Hospital);   // a bench in a cell, as in the game's own waiting rooms
            if (!Skip(state))
            {
                yield break;
            }
            if (listed)
            {
                _panel.Mount();   // the skip hides the HUD: the list stands on the skip's own screen
            }
            else
            {
                _panel.SetStatus(sheet);   // no list: the charges are told once
            }
            if (_faded)
            {
                // The cell is the screen now; the game's black goes from under it.
                yield return UiFader.UnFade(0.2f);
                _faded = false;
            }

            float started = Time.unscaledTime;
            float nextLook = 0f;
            UIs uis = InstanceBehavior<UIs>.Instance;
            while (true)
            {
                _beat = Time.unscaledTime;
                if (SaveGameManager.Current == null || uis == null || uis.timeMachine == null)
                {
                    break;
                }
                float left = state.ReleaseAt - TimeHelper.NowInMinutes();
                if (left <= 0.5f || !uis.timeMachine.isRunning || Time.unscaledTime - started > HeldMost)
                {
                    break;   // served, ended by the game (it has the player now), or gone on too long
                }

                int choice = _clicked;
                _clicked = -1;
                if (choice == 0)
                {
                    _panel.SetStatus("reallife_law_cell_waiting".Tr());
                }
                else if (choice == 1 && state.Bail > 0)
                {
                    if (Crime.Pay(state.Bail, "reallife_transaction_bail"))
                    {
                        _bailed = true;
                        Logger?.Info(FormattableString.Invariant($"Law: bail of {state.Bail} posted with {left / 60f:F1} h left."));
                        break;
                    }
                    _panel.SetStatus("reallife_law_cell_bail_short".Tr());
                }
                else if (choice == 2 && !state.Lawyer && !state.Court)
                {
                    if (Crime.Pay(fee, "reallife_transaction_lawyer"))
                    {
                        state.Lawyer = true;
                        float now = TimeHelper.NowInMinutes();
                        state.ReleaseAt = now + LawBook.AfterLawyer(left);
                        Logger?.Info(FormattableString.Invariant($"Law: lawyer called for {fee}: {left / 60f:F1} h left becomes {(state.ReleaseAt - now) / 60f:F1} h."));
                        _panel.SetOption(2, "reallife_law_cell_lawyer_used".Tr(), null, false);
                        _panel.Refresh();
                        _panel.SetStatus("reallife_law_cell_lawyer_done".Tr());
                        StopSkip();
                        if (!Skip(state))
                        {
                            break;
                        }
                    }
                    else
                    {
                        _panel.SetStatus("reallife_law_cell_lawyer_short".Tr());
                    }
                }

                if (Time.unscaledTime >= nextLook)
                {
                    nextLook = Time.unscaledTime + 0.125f;
                    ShowClock(state);
                    Feed();
                    if (state.Court && !Bridges.CourtStands)
                    {
                        Logger?.Info("Law: the courts were switched off with " + (left / 60f).ToString("F1", CultureInfo.InvariantCulture) + " h of their hold left; the player is let out.");
                        break;
                    }
                }
                yield return null;
            }
            StopSkip();

            // The game puts its own screens back a moment after a skip: the cell stays up until it has.
            float settle = 0f;
            while (uis != null && uis.timeMachine != null && (uis.timeMachine.isRunning || uis.timeMachine.isBlockingUi) && settle < 4f)
            {
                settle += Time.unscaledDeltaTime;
                _beat = Time.unscaledTime;
                yield return null;
            }
        }

        private static int LawyerFee => Settings.LawReputation && Reputation.Liked ? LawBook.LawyerFeeLiked : LawBook.LawyerFee;

        private void ShowClock(CustodyState state)
        {
            int minutes = Mathf.Max(0, Mathf.CeilToInt(state.ReleaseAt - TimeHelper.NowInMinutes()));
            _panel.SetClock(minutes / 60, minutes % 60);
        }

        private bool Skip(CustodyState state)
        {
            UIs uis = InstanceBehavior<UIs>.Instance;
            if (uis == null || uis.timeMachine == null || uis.timeMachine.isRunning || state.ReleaseAt - TimeHelper.NowInMinutes() <= 0.5f)
            {
                return false;
            }
            // In a real cell the room stays in view: no blur.
            uis.timeMachine.StartTimeMachine(new Timestamp(state.ReleaseAt), disableCancel: true, "reallife_crime_custody_info", showBlur: !_inCell);
            _skipping = true;
            return true;
        }

        // Ends a time skip of ours at once (with no delay it finishes inside the call).
        private void StopSkip()
        {
            if (!_skipping)
            {
                return;
            }
            _skipping = false;
            try
            {
                UIs uis = InstanceBehavior<UIs>.Instance;
                if (uis != null && uis.timeMachine != null && uis.timeMachine.isRunning)
                {
                    uis.timeMachine.StopTimeMachine(0f);
                }
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        // Food is brought and there is a bench: neither need runs out in there.
        private static void Feed()
        {
            GameInstance save = SaveGameManager.Current;
            if (save == null)
            {
                return;
            }
            if (save.Hunger < 25f)
            {
                save.Hunger = LawBook.ReleaseHunger;
            }
            if (save.Energy < 20f)
            {
                save.Energy = 30f;
            }
        }

        private IEnumerator Release(bool announce)
        {
            CustodyState state = LawState.Custody;
            _phase = Phase.Releasing;
            _beat = Time.unscaledTime;
            StopSkip();
            EndRest();
            GameInstance save = SaveGameManager.Current;
            if (save != null)
            {
                save.Energy = Mathf.Max(save.Energy, LawBook.ReleaseEnergy);
                save.Hunger = Mathf.Max(save.Hunger, LawBook.ReleaseHunger);
            }
            bool shown = _panel != null && _panel.IsOpen;
            if (shown)
            {
                _faded = true;
                yield return UiFader.Fade(0.3f);
                _beat = Time.unscaledTime;
                _panel.Close();
            }

            Precinct precinct = _precincts.At(state.Precinct);
            if (InStation(precinct))
            {
                yield return ReleaseInside(state, precinct, announce);
                yield break;
            }
            _inCell = false;
            Vector3 outside = PlayerHelper.GetPosition();
            if (precinct != null && !BuildingManager.IsInsideBuilding && !PlayerHelper.IsUsingVehicle
                && JobSpots.Flat(outside, precinct.Door) > 12f && PutOutside(precinct))
            {
                outside = PlayerHelper.GetPosition();   // a load in the middle may have found them somewhere else
            }
            if (_faded)
            {
                yield return UiFader.UnFade();
                _faded = false;
            }
            _beat = Time.unscaledTime;

            int served = Served(state);
            bool court = state.Court;
            Settle(court, served);
            if (announce && SaveGameManager.Current != null)
            {
                Announce(court ? (_bailed ? "reallife_law_court_released_bail" : "reallife_law_court_released")
                    : _bailed ? "reallife_law_released_bail" : "reallife_law_released", precinct, served, state);
            }
            Logger?.Info($"Law: released after {served} h ({(_bailed ? "bail" : "served")}).");
            Check(court, "let out at the door of " + (precinct != null ? precinct.Name : "no precinct") + " after " + served + " h of " + state.Hours, true);

            string vehicleId = state.VehicleId;
            state.Clear();   // the phase stays "releasing" to the end: no new arrest starts under this one

            if (vehicleId != null && !BuildingManager.IsInsideBuilding)
            {
                yield return new WaitForSecondsRealtime(0.6f);
                BringCar(vehicleId, outside);
            }
        }

        // Let go inside the station: out of the cell to the desk under the dark screen, signed out, then out of the door on
        // their own feet. The car left at the scene is at the pound: any front desk has it brought round.
        private IEnumerator ReleaseInside(CustodyState state, Precinct precinct, bool announce)
        {
            if (!_faded)
            {
                _faded = true;
                yield return UiFader.Fade(0.3f);
                _beat = Time.unscaledTime;
            }
            EndScene();
            LeaveCell();
            yield return null;
            if (_faded)
            {
                yield return UiFader.UnFade();
                _faded = false;
            }
            _beat = Time.unscaledTime;

            int served = Served(state);
            bool court = state.Court;
            Settle(court, served);
            if (announce && SaveGameManager.Current != null)
            {
                Announce(court ? (_bailed ? "reallife_law_court_released_desk_bail" : "reallife_law_court_released_desk")
                    : _bailed ? "reallife_law_released_desk_bail" : "reallife_law_released_desk", precinct, served, state);
            }
            Logger?.Info($"Law: released at the front desk after {served} h ({(_bailed ? "bail" : "served")}).");
            Check(court, "signed out at the front desk of " + (precinct != null ? precinct.Name : "no precinct") + " after " + served + " h of " + state.Hours
                         + ", then the door", true);
            string vehicleId = state.VehicleId;
            state.Clear();
            if (vehicleId != null)
            {
                LawState.ImpoundVehicleId = vehicleId;
                if (SaveGameManager.Current != null)
                {
                    Crime.ShowAmount(NotificationType.Info, "reallife_law_impound_held", LawBook.ImpoundFee, 12f);
                }
            }
            if (_interior != null && _interior.Dressed && ClipLibrary.Has("serve"))
            {
                _interior.Sergeant.Act("serve");
            }
            yield return Wait(2.2f);
            PlayerController player = PlayerHelper.PlayerController;
            if (player != null && InStation(precinct) && _interior != null && _interior.WayOut != Vector3.zero && !player.NavigationDisabled && LifeEvents.IsSafeForEvent())
            {
                player.SetNewDestination(_interior.WayOut, false);
            }
        }

        // Hours served: in a Going Public session the whole term, as it was served at once (Cell).
        private int Served(CustodyState state)
        {
            int served = Mathf.Clamp(Mathf.RoundToInt((TimeHelper.NowInMinutes() - state.StartedAt) / 60f), 0, LawBook.MostHours);
            if (GoingPublic.ClockShared && !_bailed)
            {
                served = Mathf.Clamp(state.Hours, 0, LawBook.MostHours);
            }
            return served;
        }

        private static void Announce(string key, Precinct precinct, int served, CustodyState state)
        {
            var data = new Dictionary<string, string>(4)
            {
                { "precinct", precinct != null ? precinct.Name : string.Empty },
                { "hours", served.ToString() },
                { "amount", ((float)state.Paid).ToCurrencyFormat() },
                { "bail", ((float)state.Bail).ToCurrencyFormat() }
            };
            Notifications.Show(NotificationType.Info, key, data, 12f);
        }

        private static void BringCar(string vehicleId, Vector3 near)
        {
            try
            {
                foreach (VehicleController vehicle in VehicleHelper.AllPlayerVehicles)
                {
                    if (vehicle != null && vehicle.vehicleInstance != null && vehicle.vehicleInstance.id == vehicleId)
                    {
                        if (JobSpots.Flat(vehicle.transform.position, near) < 70f)
                        {
                            return;   // already here: they were arrested on the precinct's own street
                        }
                        VehicleTow.BringNear(vehicle.vehicleInstance, near, LawBook.ImpoundFee, "reallife_law_car_impound", "reallife_law_car_impound_shop",
                            "reallife_law_car_impound_left", "reallife_transaction_impound");
                        return;
                    }
                }
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        // What a stay leaves behind: an arrest's goes on the police record; a court's is the courts' own business.
        private void Settle(bool court, int served)
        {
            if (court)
            {
                HoldBailed = _bailed;
                ModState.AddModifier(ModState.MoodDown);
                return;
            }
            Record.Close(_bailed ? Outcome.Bailed : Outcome.Served, served);
            ModState.AddModifier(ModState.MoodDownBig);
        }

        private void EndRest()
        {
            if (!_resting)
            {
                return;
            }
            _resting = false;
            try
            {
                if (InstanceBehavior<GameManager>.Instance != null && InstanceBehavior<GameManager>.Instance.playerController != null)
                {
                    EnergyHelper.SetCurrentEnergyRegen(EnergyRegen.None);
                }
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }
    }
}
