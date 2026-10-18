using System;
using System.Collections;
using System.Collections.Generic;
using Helpers;
using UI;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul.Law
{
    /// <summary>Holds other than an arrest: handing oneself in at a front desk, and a court's order.</summary>
    public sealed partial class Custody
    {
        private Action _released;            // whoever ordered a hold is told once, however it ends

        /// <summary>Somebody who got away gives themselves up at a desk, for half the time and bail. False if custody cannot now.</summary>
        internal static bool TurnIn(int charges, Precinct precinct)
        {
            Custody custody = Instance;
            if (!Ready || charges == 0 || precinct == null || LawBook.Sentence(charges, 0, null) <= 0)
            {
                return false;
            }
            try
            {
                CustodyState state = LawState.Custody;
                int priors = Record.TimesHeld(LawBook.RepeatDays);
                state.Clear();
                state.Charges = charges;
                state.Hours = Mathf.Max(LawBook.LeastHours, Mathf.CeilToInt(LawBook.Sentence(charges, priors, Crime.Random01) * 0.5f));
                state.Bail = LawBook.Bail(charges, priors) / 2;
                state.Precinct = precinct.Index;
                LawState.WantedCharges = 0;
                custody._bailed = false;
                custody._phase = Phase.Arrest;
                custody._beat = Time.unscaledTime;
                var data = new Dictionary<string, string>(3)
                {
                    { "charges", ChargeNames(charges, ", ") }, { "hours", state.Hours.ToString() }, { "precinct", precinct.Name }
                };
                Notifications.Show(NotificationType.Warning, "reallife_law_turnin_notice", data, 12f);
                Record.Add(charges, state.Hours, 0, Outcome.Held);
                Reputation.AtTheDesk(6);   // for coming in
                Logger?.Info($"Law: handed themselves in at {precinct.Name} for {ChargeIds(charges)}: {state.Hours} h, bail {state.Bail}.");
                custody.StartCoroutine(Guard.Routine(custody.TurnInRoutine(precinct)));
                return true;
            }
            catch (Exception e)
            {
                Logger?.Error(e);
                custody.Cleanup();
                return false;
            }
        }

        private IEnumerator TurnInRoutine(Precinct precinct)
        {
            try
            {
                yield return new WaitForSecondsRealtime(1.2f);
                _beat = Time.unscaledTime;
                _faded = true;
                yield return UiFader.Fade(0.5f, "reallife_crime_custody_fade");
                _beat = Time.unscaledTime;
                _phase = Phase.Transport;
                if (InStation(precinct))
                {
                    PutInCell();
                }
                else if (!BuildingManager.IsInsideBuilding && !PlayerHelper.IsUsingVehicle && PutOutside(precinct))
                {
                    yield return GoInside();
                }
                StartTerm();
                yield return Cell();
                yield return Release(announce: true);
            }
            finally
            {
                Cleanup();
            }
        }

        /// <summary>True if the court's hold that ended last was ended by bail. Read it in the "released" call.</summary>
        public static bool HoldBailed { get; private set; }

        /// <summary>
        /// Holds the player by a court's order, in a precinct's cell as after an arrest but with no scene, sheet, lawyer or
        /// line on the record. <paramref name="hours"/> are game hours; a <paramref name="bail"/> of 0 offers none;
        /// <paramref name="released"/> is called once however it ends, and not kept across a save. False if custody cannot.
        /// </summary>
        public static bool Hold(float hours, string reasonKey, int bail, Action released)
        {
            Custody custody = Instance;
            if (!Ready || hours <= 0f || !Bridges.CourtStands)
            {
                return false;
            }
            try
            {
                CustodyState state = LawState.Custody;
                // In a station already (a bail hearing is often at its desk): that one; otherwise the nearest.
                Precinct precinct = LawDirector.Inside ?? custody._precincts.Nearest(PlayerHelper.GetPosition());
                state.Clear();
                state.Court = true;
                state.Reason = reasonKey;
                state.Hours = Mathf.Clamp(Mathf.CeilToInt(hours), LawBook.LeastHours, LawBook.MostHours);
                state.Bail = Mathf.Max(0, bail);
                state.Precinct = precinct != null ? precinct.Index : -1;
                HoldBailed = false;
                custody._bailed = false;
                custody._released = released;
                custody._phase = Phase.Transport;
                custody._beat = Time.unscaledTime;
                var data = new Dictionary<string, string>(2)
                {
                    { "hours", state.Hours.ToString() }, { "precinct", precinct != null ? precinct.Name : "reallife_law_precinct_city".Tr() }
                };
                Notifications.Show(NotificationType.Warning, "reallife_law_court_notice", data, 12f);
                Logger?.Info($"Law: held by order of a court ({reasonKey}): {state.Hours} h at {(precinct != null ? precinct.Name : "no precinct")}, bail {state.Bail}.");
                custody.StartCoroutine(Guard.Routine(custody.HoldRoutine(precinct)));
                return true;
            }
            catch (Exception e)
            {
                Logger?.Error(e);
                custody._released = null;   // the caller holds them its own way
                LawState.Custody.Clear();
                custody.Cleanup();
                return false;
            }
        }

        private IEnumerator HoldRoutine(Precinct precinct)
        {
            try
            {
                yield return new WaitForSecondsRealtime(1.2f);
                _beat = Time.unscaledTime;
                // Never over a dialog, a menu or another time skip: it waits, alive, and the hours start when it can.
                float waited = 0f;
                while (!LifeEvents.IsSafeForEvent() && waited < SafeWait)
                {
                    waited += Time.unscaledDeltaTime;
                    _beat = Time.unscaledTime;
                    yield return null;
                }
                if (SaveGameManager.Current == null)
                {
                    yield break;
                }
                // The car they sit in is remembered, as at an arrest: their own only, never another player's.
                var gm = InstanceBehavior<GameManager>.Instance;
                VehicleInstance car = gm != null && PlayerHelper.IsUsingVehicle ? VehicleTow.LeftBehind(gm.selectedVehicle) : null;
                if (car != null)
                {
                    LawState.Custody.VehicleId = car.id;
                }
                _faded = true;
                yield return UiFader.Fade(0.7f, "reallife_crime_custody_fade");
                _beat = Time.unscaledTime;
                _moved = false;
                if (InStation(precinct))
                {
                    PutInCell();
                    Logger?.Info("Law: held in " + precinct.Name + (_inCell ? ": in the holding cell." : ": no cell in this room; held at the desk."));
                }
                else
                {
                    yield return Transport();
                    if (_moved)
                    {
                        yield return GoInside();
                    }
                }
                bool inside = InStation(precinct);
                Check(true, "taken to " + (precinct != null ? precinct.Name : "no precinct") + ": "
                            + (_inCell ? "in the holding cell" : inside ? "no cell in this room, held at the desk" : _moved ? "held outside the door" : "could not be moved, held where they are"),
                    _inCell || inside || _moved);

                StartTerm();
                yield return Cell();
                yield return Release(announce: true);
            }
            finally
            {
                Cleanup();
            }
        }

        // The cell's note for a court's order: the caller's own sentence, or the plain one.
        private static string Order(CustodyState state)
        {
            string key = string.IsNullOrEmpty(state.Reason) ? "reallife_law_cell_order_plain" : state.Reason;
            string text = key.Tr();
            return string.IsNullOrEmpty(text) || text == key ? "reallife_law_cell_order_plain".Tr() : text;
        }

        // One line per step of a court's hold, with Developer diagnostics on.
        private static void Check(bool court, string what, bool pass)
        {
            if (court && Meta.Dev.Verbose)
            {
                Logger?.Info("Law check (court hold): " + what + ": " + (pass ? "PASS" : "FAIL") + ".");
            }
        }
    }
}
