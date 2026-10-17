using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using BAModAPI;
using BigAmbitions.DayNightCycle;
using Extensions;
using GleyTrafficSystem;
using Helpers;
using Localizor;
using Parking.UndergroundParking;
using UI;
using UI.Notification;
using UnityEngine;
using Random = UnityEngine.Random;

namespace RealLifeOverhaul.Law
{
    /// <summary>
    /// Being arrested and held, from the hand on the shoulder to the pavement outside the
    /// precinct. One arrest at a time; whoever caught the player (Crime/PoliceAttention,
    /// Traffic/PolicePursuit, the routes in <see cref="Arrests"/>) has already charged the fines
    /// and hands over the charge sheet.
    ///   on the street  the charge and the sentence are announced first. The player is taken out
    ///                  of a car that has stopped, held where they stand, and an officer walks up
    ///                  behind them (from the patrol car, or from round the corner): hands up,
    ///                  then handcuffs (the clips and CuffPose every arrest in the mod uses, on
    ///                  the player's own body the way Jobs/JobBody plays clips on it). Where no
    ///                  officer can be had, or the player is indoors, it is the fade alone.
    ///   the ride       the screen goes dark; the player is put on the pavement outside the
    ///                  nearest precinct at once, so that whatever happens next (a save, the mod
    ///                  being removed) finds them standing in a street.
    ///   the cell       the game's list window (CustodyList) while the game's own time
    ///                  skip runs the clock to the release time: businesses, bills and the rest of
    ///                  the simulation go on as in any time skip. Energy comes back as on a bench
    ///                  and food is brought, so nobody collapses in there. Bail (not for a death)
    ///                  ends it at once; a lawyer, once, strikes off a third of what is left.
    ///   release        outside the door, facing the street, with the summary; the car is brought
    ///                  from the impound to a kerb space nearby (Driving/VehicleTow).
    /// A court's order (<see cref="Hold"/>: remand, a term of custody) is served the same way, in the same cell, with no
    /// arrest played, no charge sheet, no lawyer and no line on the police record.
    /// The stay is in the save (LawState.Custody): a load in the middle resumes what is left of
    /// it, or lets the player go if the time has passed. A watchdog in LawDirector lets them go
    /// if this ever stops making progress. Idle, this component does nothing at all: it has no
    /// Update, only the coroutine of the arrest that is on.
    /// </summary>
    public sealed class Custody : MonoBehaviour
    {
        private const NavigationBlocker Blocker = NavigationBlocker.DoctorOperation;   // any blocker stops the player; this one nothing else on a street sets
        private const string CuffingClip = SceneClips.ArrestCuffed;     // in the bundle since 0.44; where it cannot be had the cuffs simply go on
        private const string OfficerCuffClip = SceneClips.OfficerCuff;  // the officer's half of it: whole body, from 0.9 m straight behind, its end held
        private const float StalledAfter = 25f;      // real seconds without a sign of life before the watchdog steps in
        private const float HeldMost = 420f;         // real seconds a stay may take on screen, whatever the clock says
        private const float SafeWait = 40f;          // real seconds an arrest waits for a dialog or a menu to close before it is dropped

        private enum Phase { None, Arrest, Transport, Held, Releasing }

        public static IModLogger Logger;
        public static Custody Instance { get; private set; }

        /// <summary>True from the arrest until the player is back on the street. Safe to read at any time.</summary>
        public static bool Active => Instance != null && Instance._phase != Phase.None;

        /// <summary>True while the arrest is being played where it happened: whoever owns the patrol car keeps it there until this is false.</summary>
        public static bool OnScene => Instance != null && Instance._phase == Phase.Arrest;

        /// <summary>True if an arrest made now would end in a cell (the option is on, the save is loaded, there is a precinct, nobody is being held).</summary>
        public static bool Ready =>
            Settings.LawCustody && Instance != null && Instance.isActiveAndEnabled && LawState.Loaded && !Active
            && Instance._precincts != null && Instance._precincts.Count > 0 && SaveGameManager.Current != null;

        private readonly ActorRole _officer = new ActorRole();
        private readonly StringBuilder _text = new StringBuilder(256);
        private Precincts _precincts;
        private StationInterior _interior;
        private CustodyList _panel;
        private bool _inCell;                // the player is standing in a station's holding cell, in view
        private Phase _phase = Phase.None;
        private float _beat;                 // the last real time the routine was alive
        private bool _held;                  // the player is kept on the spot
        private bool _cuffed;
        private bool _clip;                  // one of our clips is on the player
        private bool _faded;
        private bool _resting;
        private bool _skipping;              // the time skip that is running is ours
        private bool _bailed;
        private int _clicked = -1;
        private Action<int> _onClick;
        private PlayerController _player;
        private Action _released;            // whoever ordered a hold is told once, however it ends
        private bool _moved;                 // the player was put down outside the precinct

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

        // ---------------------------------------------------------------- the arrest

        /// <summary>
        /// Arrests the player on these charges. <paramref name="paid"/> is what the fines and damages
        /// came to, already charged by the caller (shown on the charge sheet, never charged again).
        /// Returns false, and does nothing, if custody is off or somebody is already being held:
        /// the caller then does what it did before there were precincts.
        /// </summary>
        /// <param name="patrol">The patrol car that made the stop, or null: its officer is the one who walks up.</param>
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
            LawState.WantedCharges = 0;   // whatever they were wanted for is settled with this
            _bailed = false;
            _phase = Phase.Arrest;
            _beat = Time.unscaledTime;

            // The charge and the sentence, before anything else happens.
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

                // Never over a dialog, a menu, another time skip or a hospital trip: wait for it, and if it does not end it is the fine alone.
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
                    yield break;   // (dropped: they could not be moved)
                }
                yield return GoInside();

                LawState.Custody.StartedAt = TimeHelper.NowInMinutes();
                LawState.Custody.ReleaseAt = LawState.Custody.StartedAt + LawState.Custody.Hours * 60f;
                LawState.Custody.Active = true;
                _phase = Phase.Held;
                yield return Cell();
                yield return Release(announce: true);
            }
            finally
            {
                Cleanup();
            }
        }

        // The arrest where it happened: out of the car, an officer behind them, hands up, cuffs.
        private IEnumerator Scene(VehicleComponent patrol)
        {
            var gm = InstanceBehavior<GameManager>.Instance;
            _player = PlayerHelper.PlayerController;
            if (gm == null || _player == null)
            {
                yield break;
            }

            // The car they are in is remembered, so that it can be brought to the precinct afterwards: their own only, never
            // another player's car of a Going Public session (VehicleTow.LeftBehind).
            VehicleInstance car = PlayerHelper.IsUsingVehicle ? VehicleTow.LeftBehind(gm.selectedVehicle) : null;
            if (car != null)
            {
                LawState.Custody.VehicleId = car.id;
            }
            yield return LeaveVehicle(4f, force: false);
            if (PlayerHelper.IsUsingVehicle || BuildingManager.IsInsideBuilding || !Settings.People || _player.Character == null
                || RagdollSystem.Holds(_player.Character))
            {
                yield break;   // still rolling, indoors, or knocked down: nothing is played, the fade follows
            }

            HoldPlayer();
            Vector3 at = _player.transform.position;
            Vector3 forward = _player.transform.forward;
            forward.y = 0f;
            forward = forward.sqrMagnitude < 0.01f ? Vector3.forward : forward.normalized;
            Vector3 right = Vector3.Cross(Vector3.up, forward);

            // Where the officer comes from: a door of the patrol car, or pavement out of view.
            Vector3 from = default;
            bool fromCar = false;
            if (patrol != null)
            {
                fromCar = StreetSpots.BesideCar(patrol, StreetSpots.Left, 1f, out from) || StreetSpots.BesideCar(patrol, StreetSpots.Right, 1f, out from);
            }
            bool origin = fromCar || StreetSpots.PavementAround(at, 9f, 17f, true, out from);

            // Where the officer ends up: a pace behind them, or to a side; never in a vehicle, never on top of them.
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
            // (To that place itself, not a step short of it: the officer's clip is measured from 0.9 m off.)
            _officer.GoExactly(stand, JobSpots.Flat(from, at) > 14f ? Pace.Jog : Pace.Walk, at, 16f);

            // Hands up while the officer comes: raised in one clip, kept up in another (the pair every arrest in the mod uses).
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
                        Play(SceneClips.HandsUpHold, ClipEnd.Return);   // the loop that keeps them up
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
                yield break;   // taken back by the pool for something else: straight to the fade
            }

            // Cuffed. The arms are the clip's where it can be had, and CuffPose's otherwise; the handcuffs on the wrists are CuffPose's either way.
            if (JobSpots.Flat(_officer.Position, at) < 2.2f && !Interrupted())
            {
                _officer.Stand(at);
                // With the two clips made for it the arms are brought down and the officer puts the cuffs on. They are made
                // for the officer 0.9 m STRAIGHT BEHIND, both facing the same way, started together: so the player is turned
                // to have their back to wherever the officer came to stand, and the officer's half is played only from that
                // distance (anywhere else it is the reach every arrest had). Where the clips cannot be had it is the cut to
                // the cuffed stance.
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
                        // Both in this frame. The officer's is whole-body and ends stepped in with a hand on the shoulder: held.
                        bool officerPlays = pair && _officer.PlaceAt(at + back * SceneClips.CuffBehind, at) && _officer.Act(OfficerCuffClip, ClipEnd.Hold);
                        if (!officerPlays)
                        {
                            _officer.Gesture(CrimeSceneGestures.Reach);
                        }
                        Logger?.Info(FormattableString.Invariant($"Law: cuffed in the clip, the officer {gap:F2} m behind (the pair is made for {SceneClips.CuffBehind:F2}): ")
                                     + (officerPlays ? "the officer's half is played." : "the officer reaches instead."));
                        float click = Mathf.Clamp(cuffing.Mark(SceneClips.CuffClick, cuffing.Length * 0.6f), 0.1f, Mathf.Max(0.1f, cuffing.Length));
                        yield return Wait(click);
                        // The click: the pair on the wrists is shown and the one in the officer's hand put away, so one pair is ever seen.
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

        // The held player turned on the spot to face a way (their back to the officer), over a moment: per frame only while
        // it turns, and given up after a second if something else has the body's heading.
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

        // Out of whatever they are driving, once it has stopped. With force (only ever under the dark screen) also out of a
        // car they would not stop: the game's own exit halts it where it is, and the impound brings it to the precinct later.
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

            // The game tracks "driving" in two places; the selected vehicle is only cleared when its HUD panel closes (Life/LifeEvents).
            UIs hud = InstanceBehavior<UIs>.Instance;
            if (gm.selectedVehicle != null && !PlayerHelper.IsUsingVehicle && hud != null && hud.playerHUD != null && hud.playerHUD.itemPanelUI != null)
            {
                hud.playerHUD.itemPanelUI.Toggle(false);
            }
        }

        // Under the dark screen: out of any car or building, and onto the pavement outside the precinct.
        private IEnumerator Transport()
        {
            yield return LeaveVehicle(3f, force: true);
            var buildings = InstanceBehavior<BuildingManager>.Instance;
            if (UndergroundParkingManager.IsInsideParking)
            {
                UndergroundParkingManager.ExitParking(playFadeAnimation: false);   // (as the game's own hospital trip leaves a car park)
                yield return new WaitForSecondsRealtime(0.5f);
            }
            if (BuildingManager.IsInsideBuilding && buildings != null)
            {
                buildings.ExitFromBuilding(0, playFadeAnimation: false);
                float waited = 0f;
                while ((BuildingManager.IsInsideBuilding || buildings.exitingBuilding) && waited < 10f)
                {
                    waited += Time.unscaledDeltaTime;
                    _beat = Time.unscaledTime;
                    yield return null;
                }
                yield return new WaitForSecondsRealtime(0.3f);
            }
            // Knocked down, or on the way to hospital: they are the game's or the ragdoll's for now, and are not moved.
            PlayerController held = PlayerHelper.PlayerController;
            float patience = 0f;
            while ((!LifeEvents.IsSafeForEvent() || (held != null && held.Character != null && RagdollSystem.Holds(held.Character))) && patience < 8f)
            {
                patience += Time.unscaledDeltaTime;
                _beat = Time.unscaledTime;
                yield return null;
            }
            _beat = Time.unscaledTime;
            if (!LifeEvents.IsSafeForEvent() || held == null || held.Character == null || RagdollSystem.Holds(held.Character)
                || PlayerHelper.IsUsingVehicle || BuildingManager.IsInsideBuilding || UndergroundParkingManager.IsInsideParking
                || !PutOutside(_precincts.At(LawState.Custody.Precinct)))
            {
                _moved = false;
                if (LawState.Custody.Court)
                {
                    // A court's hours are not let off because a door could not be reached: they pass where the player is.
                    Logger?.Warn("Law: the player could not be taken to the precinct (still in a vehicle or a building, or no way to the door); the court's hours are served where they are.");
                    yield break;
                }
                Logger?.Warn("Law: the player could not be taken to the precinct (still in a vehicle or a building, or no way to the door); let go with the fines.");
                Drop();
                yield break;
            }
            _moved = true;
        }

        // On the pavement in front of the precinct's door, facing the street. False if they could not be put there.
        private bool PutOutside(Precinct precinct)
        {
            PlayerController player = PlayerHelper.PlayerController;
            if (precinct == null || player == null || player.Character == null || player.Character.navmeshAgent == null)
            {
                return false;
            }
            _precincts.DressNow(precinct);
            Vector3 outward;
            if (!Precincts.Front(precinct, out Vector3 spot, out outward))
            {
                // The game's own place for somebody arriving at this building (its taxi puts them there).
                spot = precinct.Building.GetNavMeshTargetPosition();
                outward = spot - precinct.Door;
                outward.y = 0f;
                if (!UnityEngine.AI.NavMesh.SamplePosition(spot, out _, 3f, -1))
                {
                    return false;
                }
            }
            player.ResetNavigation();
            player.Character.WarpSafely(spot);
            if (outward.sqrMagnitude > 0.01f)
            {
                player.transform.rotation = Quaternion.LookRotation(outward.normalized, Vector3.up);
            }
            player.Character.Reset();
            player.ResetNavigation();
            return JobSpots.Flat(player.transform.position, spot) < 4f;
        }

        // From the pavement into the station, where it is one that can be walked into: through the building's own door by the
        // game's own way in (which fades and clears by itself), and into the holding cell before the screen clears. Where
        // any of that cannot be had the player stays where they are and the cell is the panel, as it always was.
        private IEnumerator GoInside()
        {
            _inCell = false;
            Precinct precinct = _precincts.At(LawState.Custody.Precinct);
            LawDirector director = LawDirector.Instance;
            BuildingManager buildings = InstanceBehavior<BuildingManager>.Instance;
            if (_interior == null || director == null || buildings == null || !Precincts.CanEnter(precinct) || !director.EnterStation(precinct))
            {
                yield break;
            }
            float waited = 0f;
            bool placed = false;
            while (waited < 12f && (buildings.enteringBuilding || !BuildingManager.IsInsideBuilding))
            {
                waited += Time.unscaledDeltaTime;
                _beat = Time.unscaledTime;
                if (!placed && _interior.Dressed && _interior.Precinct == precinct)
                {
                    placed = true;
                    PutInCell();
                }
                yield return null;
            }
            _beat = Time.unscaledTime;
            if (waited < 12f)
            {
                _faded = false;   // (the game's own way in has cleared the screen)
            }
            if (!placed && InStation(precinct))
            {
                // The room is furnished a moment after the door: looked for once more.
                for (int i = 0; i < 30 && !_interior.Dressed; i++)
                {
                    _beat = Time.unscaledTime;
                    yield return null;
                }
                PutInCell();
            }
            Logger?.Info("Law: taken inside " + (precinct != null ? precinct.Name : "the precinct") + (_inCell ? ": in the holding cell." : InStation(precinct) ? ": no cell in this room; held at the desk." : ": the way in failed; held outside."));
        }

        private bool InStation(Precinct precinct)
        {
            return precinct != null && precinct.Building != null && BuildingManager.IsInsideBuilding
                   && ReferenceEquals(InstanceBehavior<BuildingManager>.Instance.building, precinct.Building.building);
        }

        // Behind the bars, facing them, and kept there. False if the room has no cell.
        private bool PutInCell()
        {
            PlayerController player = PlayerHelper.PlayerController;
            if (_interior == null || !_interior.Dressed || !_interior.HasCell || player == null || player.Character == null || player.Character.navmeshAgent == null)
            {
                return false;
            }
            _player = player;
            player.ResetNavigation();
            player.Character.navmeshAgent.Warp(_interior.CellSpot);
            Vector3 facing = _interior.CellFace - _interior.CellSpot;
            facing.y = 0f;
            if (facing.sqrMagnitude > 0.01f)
            {
                player.transform.rotation = Quaternion.LookRotation(facing.normalized, Vector3.up);
            }
            player.Character.Reset();
            HoldPlayer();
            _inCell = true;
            return true;
        }

        // Out of the cell to the front desk (the bars are solid: nobody walks out of it). If they cannot be put there the
        // bars are taken away instead, so that nobody is ever left shut in.
        private void LeaveCell()
        {
            if (!_inCell)
            {
                return;
            }
            _inCell = false;
            try
            {
                PlayerController player = PlayerHelper.PlayerController;
                if (_interior == null || !_interior.Dressed || player == null || player.Character == null || player.Character.navmeshAgent == null
                    || !BuildingManager.IsInsideBuilding)
                {
                    return;
                }
                player.ResetNavigation();
                if (UnityEngine.AI.NavMesh.SamplePosition(_interior.ServiceSpot, out UnityEngine.AI.NavMeshHit hit, 1.5f, -1))
                {
                    player.Character.navmeshAgent.Warp(hit.position);
                    Vector3 facing = _interior.ServiceFace - hit.position;
                    facing.y = 0f;
                    if (facing.sqrMagnitude > 0.01f)
                    {
                        player.transform.rotation = Quaternion.LookRotation(facing.normalized, Vector3.up);
                    }
                    player.Character.Reset();
                }
                else
                {
                    _interior.OpenCell();
                }
            }
            catch (Exception e)
            {
                Logger?.Error(e);
                _interior?.OpenCell();
            }
        }

        // Nothing came of it: the fines stand, the cell does not. Leaves the record as a citation.
        private void Drop()
        {
            Record.Close(Outcome.Cited, 0);
            LawState.Custody.Clear();
            _phase = Phase.None;
            if (SaveGameManager.Current != null)
            {
                Notifications.Show(NotificationType.Warning, "reallife_law_arrest_dropped", null, 9f);
            }
        }

        // ---------------------------------------------------------------- the cell

        // The panel over the dark screen while the game's own time skip runs the clock.
        private IEnumerator Cell()
        {
            CustodyState state = LawState.Custody;
            if (GoingPublic.ClockShared)
            {
                // In a Going Public session the clock is everyone's and passes no faster for one player (Core/GoingPublic):
                // the hours are served at once. The record, the fines and the court's order stand as they are.
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
                // A court's order: sitting it out, and bail only if the court set one. No lawyer shortens it.
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
            EnergyHelper.SetCurrentEnergyRegen(EnergyRegen.Hospital);   // a bench in a cell: a little rest, as in the game's own waiting rooms
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
                _panel.SetStatus(sheet);   // no list: the sentence is served, and the charges are told once
            }
            if (_faded)
            {
                // The cell is the screen now; the game's own black goes from under it.
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
                    break;   // served; or the game ended the skip for reasons of its own (it has the player now); or this has gone on too long
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

            // The game puts its own screens back a moment after a skip ends: the cell stays up until it has.
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

        // Starts the game's time skip to the release time. False if it could not be started.
        private bool Skip(CustodyState state)
        {
            UIs uis = InstanceBehavior<UIs>.Instance;
            if (uis == null || uis.timeMachine == null || uis.timeMachine.isRunning || state.ReleaseAt - TimeHelper.NowInMinutes() <= 0.5f)
            {
                return false;
            }
            // (In a real cell the room stays in view: the game's blur is left off.)
            uis.timeMachine.StartTimeMachine(new Timestamp(state.ReleaseAt), disableCancel: true, "reallife_crime_custody_info", showBlur: !_inCell);
            _skipping = true;
            return true;
        }

        // Ends a time skip of ours at once (the game's own call; with no delay it finishes inside the call).
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

        // ---------------------------------------------------------------- release

        private IEnumerator Release(bool announce)
        {
            CustodyState state = LawState.Custody;
            _phase = Phase.Releasing;
            _beat = Time.unscaledTime;
            StopSkip();
            Rest(false);
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
                outside = PlayerHelper.GetPosition();   // (a load in the middle may have found them somewhere else)
            }
            if (_faded)
            {
                yield return UiFader.UnFade();
                _faded = false;
            }
            _beat = Time.unscaledTime;

            int served = Mathf.Clamp(Mathf.RoundToInt((TimeHelper.NowInMinutes() - state.StartedAt) / 60f), 0, LawBook.MostHours);
            if (GoingPublic.ClockShared && !_bailed)
            {
                served = Mathf.Clamp(state.Hours, 0, LawBook.MostHours);   // served at once in a Going Public session (Cell): the whole term counts
            }
            bool court = state.Court;
            Settle(court, served);
            if (announce && SaveGameManager.Current != null)
            {
                var data = new Dictionary<string, string>(4)
                {
                    { "precinct", precinct != null ? precinct.Name : string.Empty },
                    { "hours", served.ToString() },
                    { "amount", ((float)state.Paid).ToCurrencyFormat() },
                    { "bail", ((float)state.Bail).ToCurrencyFormat() }
                };
                Notifications.Show(NotificationType.Info, court ? (_bailed ? "reallife_law_court_released_bail" : "reallife_law_court_released")
                    : _bailed ? "reallife_law_released_bail" : "reallife_law_released", data, 12f);
            }
            Logger?.Info($"Law: released after {served} h ({(_bailed ? "bail" : "served")}).");
            Check(court, "let out at the door of " + (precinct != null ? precinct.Name : "no precinct") + " after " + served + " h of " + state.Hours, true);

            string vehicleId = state.VehicleId;
            state.Clear();   // (the phase stays "releasing" until the routine's last line: no new arrest starts under this one)

            // Their car is waiting in a space nearby, for the impound fee.
            if (vehicleId != null && !BuildingManager.IsInsideBuilding)
            {
                yield return new WaitForSecondsRealtime(0.6f);
                BringCar(vehicleId, outside);
            }
        }

        // Let go inside the station: out of the cell to the front desk under the dark screen, signed out there, and then out
        // through the front door on their own feet. The car that was left at the scene is at the pound: any front desk has it
        // brought round.
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

            int served = Mathf.Clamp(Mathf.RoundToInt((TimeHelper.NowInMinutes() - state.StartedAt) / 60f), 0, LawBook.MostHours);
            if (GoingPublic.ClockShared && !_bailed)
            {
                served = Mathf.Clamp(state.Hours, 0, LawBook.MostHours);   // served at once in a Going Public session (Cell): the whole term counts
            }
            bool court = state.Court;
            Settle(court, served);
            if (announce && SaveGameManager.Current != null)
            {
                var data = new Dictionary<string, string>(4)
                {
                    { "precinct", precinct != null ? precinct.Name : string.Empty },
                    { "hours", served.ToString() },
                    { "amount", ((float)state.Paid).ToCurrencyFormat() },
                    { "bail", ((float)state.Bail).ToCurrencyFormat() }
                };
                Notifications.Show(NotificationType.Info, court ? (_bailed ? "reallife_law_court_released_desk_bail" : "reallife_law_court_released_desk")
                    : _bailed ? "reallife_law_released_desk_bail" : "reallife_law_released_desk", data, 12f);
            }
            Logger?.Info($"Law: released at the front desk after {served} h ({(_bailed ? "bail" : "served")}).");
            Check(court, "signed out at the front desk of " + (precinct != null ? precinct.Name : "no precinct") + " after " + served + " h of " + state.Hours + ", then the door", true);
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
            // Signed out by the sergeant, then the door.
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
                            return;   // it is already here (they were arrested on the precinct's own street)
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

        // ---------------------------------------------------------------- handing oneself in

        /// <summary>
        /// Somebody who got away walks into a station and gives themselves up (the front desk): the charge they ran from,
        /// for half the time and half the bail. False, and nothing done, if custody cannot take them now.
        /// </summary>
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
                LawState.Custody.StartedAt = TimeHelper.NowInMinutes();
                LawState.Custody.ReleaseAt = LawState.Custody.StartedAt + LawState.Custody.Hours * 60f;
                LawState.Custody.Active = true;
                _phase = Phase.Held;
                yield return Cell();
                yield return Release(announce: true);
            }
            finally
            {
                Cleanup();
            }
        }

        // ---------------------------------------------------------------- a court's order

        /// <summary>True if the hold a court ordered last ended because its bail was paid. Read it from the "released" call.</summary>
        public static bool HoldBailed { get; private set; }

        /// <summary>
        /// Holds the player by order of a court (remand, a term of custody): under the dark screen they are taken to the
        /// nearest precinct exactly as after an arrest, into its holding cell where the station can be walked into and has
        /// one (else held at the desk, else outside behind the full screen), the cell's list shows why and how long, the
        /// game's own time skip serves the time, and they are let out at the station's door. Nothing is played in the
        /// street, there is no charge sheet and no lawyer, and the police record gets no line: the conviction is the
        /// courts' own.
        /// </summary>
        /// <param name="hours">Game hours; bounded to <see cref="LawBook.MostHours"/>, which is all a station ever holds anybody.</param>
        /// <param name="reasonKey">The locale key of the sentence that says why (shown in the cell).</param>
        /// <param name="bail">What walking out at once costs, or 0 when the court set no bail: the cell then offers none.</param>
        /// <param name="released">Called once when the player is out, however it ended (served, bail, a fault, the scene going). Not kept across a save.</param>
        /// <returns>False, with nothing done, if custody cannot take them now: the caller then holds them its own way.</returns>
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
                // In a station already (the bail decision is often put to them at its desk): that one. Otherwise the nearest.
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
                custody._released = null;   // (the caller is told by the answer: it holds them its own way)
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
                // The car they are sitting in is remembered, as at an arrest: it is at the pound or brought round afterwards.
                // Their own only, never another player's car of a Going Public session (VehicleTow.LeftBehind).
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

                LawState.Custody.StartedAt = TimeHelper.NowInMinutes();
                LawState.Custody.ReleaseAt = LawState.Custody.StartedAt + LawState.Custody.Hours * 60f;
                LawState.Custody.Active = true;
                _phase = Phase.Held;
                yield return Cell();
                yield return Release(announce: true);
            }
            finally
            {
                Cleanup();
            }
        }

        // What the cell's note says for a court's order: the caller's own sentence, or the plain one where it gave none.
        private static string Order(CustodyState state)
        {
            string key = string.IsNullOrEmpty(state.Reason) ? "reallife_law_cell_order_plain" : state.Reason;
            string text = key.Tr();
            return string.IsNullOrEmpty(text) || text == key ? "reallife_law_cell_order_plain".Tr() : text;
        }

        // What a stay leaves behind when it ends. An arrest's goes on the police record and weighs on the mood; a court's
        // is the courts' own business (their file has the conviction), so the record is left alone.
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

        // One line per step of a court's hold, with "Developer diagnostics" on.
        private static void Check(bool court, string what, bool pass)
        {
            if (court && Meta.Dev.Verbose)
            {
                Logger?.Info("Law check (court hold): " + what + ": " + (pass ? "PASS" : "FAIL") + ".");
            }
        }

        // ---------------------------------------------------------------- a load in the middle

        /// <summary>Called once the save has loaded with a stay still on it: what is left is served, or the player is let go.</summary>
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
                // Not over the game's own loading, a menu or a dialog: it waits, alive, for as long as that takes.
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
                            && (!state.Court || Bridges.CourtStands);   // (a court's hold needs its court: gone or switched off, they are let out)
                Logger?.Info(FormattableString.Invariant($"Law: a stay in the cell was on the save with {left / 60f:F1} h left: {(sane ? "resumed" : "released")}."));
                Check(state.Court, "a save made during the hold was loaded with " + (left / 60f).ToString("F1", CultureInfo.InvariantCulture) + " h left: " + (sane ? "resumed" : "let out"), true);
                if (sane)
                {
                    _faded = true;
                    yield return UiFader.Fade(0.4f, "reallife_crime_custody_fade");
                    if (inside)
                    {
                        // Saved in the cell: the room is furnished a moment after the load, and they are put back behind the bars.
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

        // ---------------------------------------------------------------- safety

        /// <summary>True if an arrest or a stay is on and its routine has shown no sign of life for a while.</summary>
        internal bool Stalled => _phase != Phase.None && Time.unscaledTime - _beat > StalledAfter;

        /// <summary>
        /// The way out when something has gone wrong: everything this component changed is put back
        /// and the player is free, where they stand, now. Safe at any time.
        /// </summary>
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

        // Everything this component may have changed, put back. Safe to call more than once, from the routine's finally and from OnDestroy.
        private void Cleanup()
        {
            try
            {
                EndScene();
                LeaveCell();
                StopSkip();
                Rest(false);
                if (_panel != null)
                {
                    _panel.Close();
                }
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
            // Whoever ordered a hold hears that it is over, once, on every way out (the last line: nothing of ours is left on).
            Action released = _released;
            _released = null;
            if (released != null)
            {
                if (LawState.Custody.Court)
                {
                    LawState.Custody.Clear();   // (ended before its release ran: a fault, the scene going. Nothing is left to resume.)
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

        private void Rest(bool on)
        {
            if (_resting && !on)
            {
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
                StopClip(atOnce: true);   // (this is under the dark screen or on a fault: no fade, which a paused game would never finish)
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

        // One of the mod's clips on the player, whole body, where they are held. False if it cannot be had.
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
                ActivityAnimators.Forget(_player.Character.animator);   // handed back to the game's own animation this instant
                return;
            }
            ActivityAnimator animator = ActivityAnimators.For(_player.Character.animator);
            if (animator != null)
            {
                animator.Stop(0.15f);
            }
        }

        // The scene cannot go on as played: they were knocked down, went into a car or a building, or something has the screen.
        private bool Interrupted()
        {
            return _player == null || _player.Character == null || RagdollSystem.Holds(_player.Character) || PlayerHelper.IsUsingVehicle
                   || BuildingManager.IsInsideBuilding || SaveGameManager.Current == null;
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

        // ---------------------------------------------------------------- words

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
