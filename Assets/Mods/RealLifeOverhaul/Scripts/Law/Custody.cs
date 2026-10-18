using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using BAModAPI;
using Extensions;
using GleyTrafficSystem;
using Helpers;
using Localizor;
using UI;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul.Law
{
    /// <summary>
    /// Being arrested and held, from the hand on the shoulder to the pavement outside the precinct, after the catcher has
    /// charged the fines; a court's order (<see cref="Hold"/>) is served the same way. The stay is in the save, and a load
    /// resumes it. Cost: no Update; only the coroutine of the stay that is on, and LawDirector's watchdog.
    /// </summary>
    public sealed partial class Custody : MonoBehaviour
    {
        private const float StalledAfter = 25f;      // real seconds without a sign of life before the watchdog steps in
        private const float SafeWait = 40f;          // real seconds an arrest waits for a dialog or a menu to close before it is dropped

        private enum Phase { None, Arrest, Transport, Held, Releasing }

        public static IModLogger Logger;
        public static Custody Instance { get; private set; }

        /// <summary>True from the arrest until the player is back on the street.</summary>
        public static bool Active => Instance != null && Instance._phase != Phase.None;

        /// <summary>True while the arrest is played where it happened: the patrol car's owner keeps it there until then.</summary>
        public static bool OnScene => Instance != null && Instance._phase == Phase.Arrest;

        /// <summary>True if an arrest made now would end in a cell.</summary>
        public static bool Ready =>
            Settings.LawCustody && Instance != null && Instance.isActiveAndEnabled && LawState.Loaded && !Active
            && Instance._precincts != null && Instance._precincts.Count > 0 && SaveGameManager.Current != null;

        private readonly StringBuilder _text = new StringBuilder(256);
        private Precincts _precincts;
        private StationInterior _interior;
        private CustodyList _panel;
        private Phase _phase = Phase.None;
        private float _beat;                 // the last real time the routine was alive
        private bool _faded;
        private bool _bailed;
        private Action<int> _onClick;

        internal void Bind(Precincts precincts, StationInterior interior)
        {
            _precincts = precincts;
            _interior = interior;
        }

        private void Awake()
        {
            Instance = this;
            _onClick = index => _clicked = index;
        }

        private void OnDestroy()
        {
            Cleanup();
            _panel?.Dispose();
            if (Instance == this)
            {
                Instance = null;
            }
        }

        /// <summary>
        /// Arrests the player; <paramref name="paid"/> was charged by the caller and is only shown. False, with nothing done, if
        /// custody cannot: the caller then does what it did before.
        /// </summary>
        public static bool Arrest(int charges, int paid, VehicleComponent patrol)
        {
            Custody custody = Instance;
            if (!Ready || LawBook.Sentence(charges, 0, null) <= 0)
            {
                return false;
            }
            try
            {
                custody.Begin(charges, paid, patrol);
                return true;
            }
            catch (Exception e)
            {
                Logger?.Error(e);
                custody.Cleanup();
                return false;
            }
        }

        private void Begin(int charges, int paid, VehicleComponent patrol)
        {
            CustodyState state = LawState.Custody;
            int priors = Record.TimesHeld(LawBook.RepeatDays);
            Precinct precinct = _precincts.Nearest(PlayerHelper.GetPosition());
            state.Clear();
            state.Charges = charges;
            state.Hours = LawBook.Sentence(charges, priors, Crime.Random01);
            state.Bail = LawBook.Bail(charges, priors);
            state.Paid = Mathf.Max(0, paid);
            state.Precinct = precinct != null ? precinct.Index : -1;
            LawState.WantedCharges = 0;   // what they were wanted for is settled with this
            _bailed = false;
            _phase = Phase.Arrest;
            _beat = Time.unscaledTime;

            var data = new Dictionary<string, string>(3)
            {
                { "charges", ChargeNames(charges, ", ") },
                { "hours", state.Hours.ToString() },
                { "precinct", precinct != null ? precinct.Name : string.Empty }
            };
            Notifications.Show(NotificationType.Error, "reallife_law_arrest_notice", data, 12f);
            Record.Add(charges, state.Hours, state.Paid, Outcome.Held);
            Logger?.Info($"Law: arrested for {ChargeIds(charges)}: {state.Hours} h at {(precinct != null ? precinct.Name : "no precinct")}, bail {state.Bail}, "
                         + $"{state.Paid} paid at the arrest, held {priors} time(s) in the last {LawBook.RepeatDays} days.");
            StartCoroutine(Guard.Routine(Routine(patrol)));
        }

        private IEnumerator Routine(VehicleComponent patrol)
        {
            try
            {
                yield return new WaitForSecondsRealtime(1.5f);
                _beat = Time.unscaledTime;

                // Never over a dialog, a menu, a time skip or a hospital trip; if it does not end, the fines alone.
                float waited = 0f;
                while (!LifeEvents.IsSafeForEvent() && waited < SafeWait)
                {
                    waited += Time.unscaledDeltaTime;
                    _beat = Time.unscaledTime;
                    yield return null;
                }
                if (!LifeEvents.IsSafeForEvent() || SaveGameManager.Current == null)
                {
                    Drop();
                    yield break;
                }

                yield return Scene(patrol);

                _faded = true;
                yield return UiFader.Fade(0.7f, "reallife_crime_custody_fade");
                _beat = Time.unscaledTime;
                EndScene();
                _phase = Phase.Transport;   // whoever brought a patrol car lets it go now, while nobody can see
                yield return Transport();
                if (_phase != Phase.Transport)
                {
                    yield break;   // dropped: they could not be moved
                }
                yield return GoInside();

                StartTerm();
                yield return Cell();
                yield return Release(announce: true);
            }
            finally
            {
                Cleanup();
            }
        }

        /// <summary>Once the save has loaded with a stay on it: what is left is served, or the player is let go.</summary>
        internal void Resume()
        {
            if (_phase != Phase.None || !LawState.Custody.Active)
            {
                return;
            }
            _phase = Phase.Held;
            _bailed = false;
            _beat = Time.unscaledTime;
            StartCoroutine(Guard.Routine(ResumeRoutine()));
        }

        private IEnumerator ResumeRoutine()
        {
            try
            {
                CustodyState state = LawState.Custody;
                // Not over the game's own loading, a menu or a dialog: it waits, alive, as long as that takes.
                var pause = new WaitForSecondsRealtime(0.5f);
                while (!LifeEvents.IsSafeForEvent())
                {
                    _beat = Time.unscaledTime;
                    yield return pause;
                }
                _beat = Time.unscaledTime;
                float left = state.ReleaseAt - TimeHelper.NowInMinutes();
                Precinct where = _precincts.At(state.Precinct);
                bool inside = InStation(where);
                bool sane = left > 1f && left <= LawBook.MostHours * 60f + 1f && Settings.LawCustody && where != null
                            && (!BuildingManager.IsInsideBuilding || inside) && !PlayerHelper.IsUsingVehicle
                            && (!state.Court || Bridges.CourtStands);   // a court's hold needs its court
                Logger?.Info(FormattableString.Invariant($"Law: a stay in the cell was on the save with {left / 60f:F1} h left: {(sane ? "resumed" : "released")}."));
                Check(state.Court, "a save made during the hold was loaded with " + (left / 60f).ToString("F1", CultureInfo.InvariantCulture) + " h left: "
                                   + (sane ? "resumed" : "let out"), true);
                if (sane)
                {
                    _faded = true;
                    yield return UiFader.Fade(0.4f, "reallife_crime_custody_fade");
                    if (inside)
                    {
                        // Saved in the cell: the room is furnished a moment after the load.
                        for (int i = 0; i < 120 && _interior != null && !_interior.Dressed; i++)
                        {
                            _beat = Time.unscaledTime;
                            yield return null;
                        }
                        PutInCell();
                    }
                    else
                    {
                        yield return GoInside();
                    }
                    yield return Cell();
                    yield return Release(announce: true);
                }
                else
                {
                    yield return Release(announce: false);
                    if (SaveGameManager.Current != null)
                    {
                        Notifications.Show(NotificationType.Info, "reallife_law_released_away", null, 9f);
                    }
                }
            }
            finally
            {
                Cleanup();
            }
        }

        /// <summary>True if a stay is on and its routine has shown no sign of life for a while.</summary>
        internal bool Stalled => _phase != Phase.None && Time.unscaledTime - _beat > StalledAfter;

        /// <summary>Ends whatever is on at once: everything put back, the player free where they stand.</summary>
        internal void ForceRelease(string why)
        {
            if (_phase == Phase.None && !LawState.Custody.Active)
            {
                return;
            }
            Logger?.Warn("Law: custody ended by the watchdog (" + why + "); the player is free where they stand.");
            StopAllCoroutines();
            bool wasHeld = LawState.Custody.Active;
            bool court = LawState.Custody.Court;
            Cleanup();
            if (!court)
            {
                Record.Close(wasHeld ? Outcome.Served : Outcome.Cited, wasHeld
                    ? Mathf.Clamp(Mathf.RoundToInt((TimeHelper.NowInMinutes() - LawState.Custody.StartedAt) / 60f), 0, LawBook.MostHours) : 0);
            }
            Check(court, "ended by the watchdog (" + why + "), the player free where they stand", true);
            LawState.Custody.Clear();
            if (SaveGameManager.Current != null)
            {
                Notifications.Show(NotificationType.Info, "reallife_law_released_away", null, 9f);
            }
        }

        // Everything this component may have changed, put back. Safe more than once (the routine's finally, OnDestroy).
        private void Cleanup()
        {
            try
            {
                EndScene();
                LeaveCell();
                StopSkip();
                EndRest();
                _panel?.Close();
                if (_faded)
                {
                    _faded = false;
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
            _phase = Phase.None;
            // Whoever ordered a hold hears it is over, once, on every way out.
            Action released = _released;
            _released = null;
            if (released != null)
            {
                if (LawState.Custody.Court)
                {
                    LawState.Custody.Clear();   // ended before its release ran: nothing is left to resume
                }
                try
                {
                    released();
                }
                catch (Exception e)
                {
                    Logger?.Error(e);
                }
            }
        }

        private void StartTerm()
        {
            LawState.Custody.StartedAt = TimeHelper.NowInMinutes();
            LawState.Custody.ReleaseAt = LawState.Custody.StartedAt + LawState.Custody.Hours * 60f;
            LawState.Custody.Active = true;
            _phase = Phase.Held;
        }

        private IEnumerator Wait(float seconds)
        {
            float until = Time.unscaledTime + seconds;
            while (Time.unscaledTime < until)
            {
                _beat = Time.unscaledTime;
                yield return null;
            }
        }

        private string Sheet(CustodyState state)
        {
            _text.Length = 0;
            for (int i = 0; i < LawBook.Count; i++)
            {
                if ((state.Charges & (1 << i)) != 0)
                {
                    _text.Append("-  ").Append(LawBook.Of((Charge)i).Name.GetLocalization()).Append('\n');
                }
            }
            if (state.Paid > 0)
            {
                _text.Append('\n').Append(Fill("reallife_law_cell_paid", "amount", ((float)state.Paid).ToCurrencyFormat()));
            }
            return _text.ToString();
        }

        internal static string ChargeNames(int charges, string between)
        {
            var text = new StringBuilder(96);
            for (int i = 0; i < LawBook.Count; i++)
            {
                if ((charges & (1 << i)) != 0)
                {
                    if (text.Length > 0)
                    {
                        text.Append(between);
                    }
                    text.Append(LawBook.Of((Charge)i).Name.GetLocalization());
                }
            }
            return text.ToString();
        }

        private static string ChargeIds(int charges)
        {
            var text = new StringBuilder(64);
            for (int i = 0; i < LawBook.Count; i++)
            {
                if ((charges & (1 << i)) != 0)
                {
                    text.Append(text.Length > 0 ? "+" : string.Empty).Append(((Charge)i).ToString());
                }
            }
            return text.ToString();
        }

        internal static string Fill(string key, string name, string value)
        {
            return key.Tr().Replace("{" + name + "}", value);
        }
    }
}
