using System;
using UnityEngine;

namespace RealLifeOverhaul.Law
{
    /// <summary>The frontage's people: officers at the door and their comings and goings.</summary>
    internal sealed partial class PrecinctFrontage
    {
        private const float OfficersWithin = 110f;   // metres from the player: officers at the door
        private const float SuspectWithin = 75f;     // metres from the player: a suspect may be walked in
        private const string GuardClip = "officer_guard_idle";   // not in the bundle yet: guard_stand stands in

        private enum Act { None, OutToCar, AtCar, BackIn, WalkIn, Suspect }

        private Act _act;
        private float _actUntil;
        private float _nextAct;
        private int _actCar;
        private ThirdPersonCharacter _cuffed;

        /// <summary>The frontage's people off the street: the player has gone in, and the same few characters are wanted inside.</summary>
        public void PeopleIn()
        {
            EndAct();
            for (int i = 0; i < _officers.Length; i++)
            {
                _officers[i].Exit();
            }
        }

        // One officer either side of the door, a step out, looking at the street; and the comings and goings. The pool may
        // take them back at any moment: whoever is gone is put out again later, out of view.
        private void TickPeople(float now, Vector3 player, bool force)
        {
            for (int i = 0; i < _officers.Length; i++)
            {
                _officers[i].Tick(now);
            }
            _mover.Tick(now);
            _suspect.Tick(now);
            float sqr = CitySpots.Flat(_front - player);
            bool near = sqr < OfficersWithin * OfficersWithin;
            if (!near || !Settings.People || !Settings.Police)
            {
                if (_act != Act.None && (!near || !_mover.Visible))
                {
                    EndAct();
                }
                for (int i = 0; i < _officers.Length; i++)
                {
                    if (_officers[i].Present && !_officers[i].Visible)
                    {
                        _officers[i].Exit();
                    }
                }
                return;
            }
            TickAct(now, sqr);
            if (now < _nextOfficers && !force)
            {
                return;
            }
            _nextOfficers = now + 12f;
            for (int i = 0; i < _officers.Length; i++)
            {
                if (_officers[i].Present)
                {
                    continue;
                }
                // The second one is not always there.
                if (i == 1 && (_dressed.Index + TimeHelper.CurrentDay) % 3 == 0)
                {
                    continue;
                }
                Vector3 place = _front + _across * (i == 0 ? -1.5f : 1.5f) - _out * 0.2f;
                if (!CitySpots.Stand(place, 0.45f, out Vector3 spot) || CitySpots.Flat(spot - player) < 1.2f * 1.2f || AtSign(spot) || (!force && StreetSpots.IsInView(spot)))
                {
                    continue;
                }
                if (!_officers[i].EnterPresence(spot, ActorLook.Officer, 0.6f, onRoad: false))
                {
                    return;   // the pool has nobody to spare just now
                }
                _officers[i].Stand(spot + _out * 8f);
                string clip = ClipLibrary.Has(GuardClip) ? GuardClip : ClipLibrary.Has("guard_stand") ? "guard_stand" : null;
                if (clip != null)
                {
                    _officers[i].Work(clip);   // standing easy; without either clip they simply stand
                }
                return;   // one new character per look
            }
        }

        // One at a time on a slow timer. Whoever goes in is gone at the door, as the game's own shoppers are.
        private void TickAct(float now, float playerSqr)
        {
            switch (_act)
            {
                case Act.None:
                    if (now >= _nextAct)
                    {
                        _nextAct = now + UnityEngine.Random.Range(55f, 95f);
                        StartAct(now, playerSqr);
                    }
                    break;

                case Act.OutToCar:
                    if (!_mover.Present)
                    {
                        EndAct();
                    }
                    else if (_mover.Settled || now >= _actUntil)
                    {
                        _mover.Stand(_carAt[_actCar]);
                        _act = Act.AtCar;
                        _actUntil = now + UnityEngine.Random.Range(14f, 22f);
                    }
                    break;

                case Act.AtCar:
                    if (!_mover.Present)
                    {
                        EndAct();
                    }
                    else if (now >= _actUntil)
                    {
                        _mover.Go(_doorSpot, Pace.Walk, _dressed.Door, 45f);
                        _act = Act.BackIn;
                        _actUntil = now + 46f;
                    }
                    break;

                case Act.BackIn:
                case Act.WalkIn:
                    if (!_mover.Present || _mover.Settled || now >= _actUntil)
                    {
                        // At the door: gone inside. Held up and out of time: taken off only if nobody is looking.
                        if (!_mover.Present || CitySpots.Flat(_mover.Position - _doorSpot) < 2.2f * 2.2f || !_mover.Visible)
                        {
                            EndAct();
                        }
                        else
                        {
                            _mover.WalkAwayFrom(_front, Pace.Walk);
                            _act = Act.None;
                        }
                    }
                    break;

                case Act.Suspect:
                    if (!_suspect.Present || !_mover.Present || _suspect.Settled || now >= _actUntil)
                    {
                        if (!_suspect.Present || !_mover.Present || CitySpots.Flat(_suspect.Position - _doorSpot) < 2.4f * 2.4f || !_suspect.Visible)
                        {
                            EndAct();
                        }
                        else
                        {
                            _actUntil = now + 10f;   // held up in view: a little longer, then whenever nobody looks
                            _suspect.Go(_doorSpot, Pace.Walk, _dressed.Door, 12f);
                        }
                    }
                    break;
            }
        }

        private void StartAct(float now, float playerSqr)
        {
            float roll = UnityEngine.Random.value;
            int car = -1;
            for (int i = 0; i < Cars; i++)
            {
                if (_carOut[i] && (car < 0 || CitySpots.Flat(_carAt[i] - _doorGround) < CitySpots.Flat(_carAt[car] - _doorGround)))
                {
                    car = i;
                }
            }
            if (roll < 0.42f && car >= 0 && StreetSpots.OnPavement(_carAt[car], 3.6f, out Vector3 byCar) && CitySpots.Reachable(_doorSpot, byCar)
                && CitySpots.ClearQuietly(_doorSpot, 0.4f))
            {
                if (_mover.EnterPresence(_doorSpot, ActorLook.Officer, 0.5f, onRoad: false))
                {
                    _mover.Go(byCar, Pace.Walk, _carAt[car], 45f);
                    _mover.Work(SceneClips.RadioTalk);
                    _act = Act.OutToCar;
                    _actCar = car;
                    _actUntil = now + 46f;
                }
                return;
            }
            if (!StreetSpots.PavementAround(_front, 28f, 46f, true, out Vector3 from) || StreetSpots.IsInView(from) || !CitySpots.Reachable(from, _doorSpot))
            {
                return;
            }
            bool withSuspect = roll > 0.78f && playerSqr < SuspectWithin * SuspectWithin && ClipLibrary.Has(SceneClips.CuffedWalk) && CuffPose.Instance != null;
            if (!withSuspect)
            {
                if (_mover.EnterPresence(from, ActorLook.Officer, 1f, onRoad: false))
                {
                    _mover.Go(_doorSpot, Pace.Walk, _dressed.Door, 70f);
                    _act = Act.WalkIn;
                    _actUntil = now + 72f;
                }
                return;
            }
            // A suspect in handcuffs walking ahead, the officer a pace behind. The suspect is not one the pool may take back
            // (the cuffs would go with them): a scene of two for a minute.
            Vector3 back = from - _doorSpot;
            back.y = 0f;
            back = back.sqrMagnitude < 0.01f ? -_out : back.normalized;
            if (!_suspect.Enter(from, ActorLook.Civilian, 1f, onRoad: false))
            {
                return;
            }
            ThirdPersonCharacter body = _suspect.Character;
            if (body == null || !CuffPose.Put(body) || !_suspect.Gait(SceneClips.CuffedWalk, SceneClips.WalkPace, SceneClips.CuffedStand)
                || !_mover.EnterPresence(from + back * 1.1f, ActorLook.Officer, 1f, onRoad: false))
            {
                if (body != null)
                {
                    CuffPose.Remove(body);
                }
                _suspect.Gait(null);
                _suspect.Exit();
                return;
            }
            _cuffed = body;
            _suspect.Go(_doorSpot, Pace.Walk, _dressed.Door, 75f);
            _mover.Go(_doorSpot + back * 1.0f, Pace.Walk, _dressed.Door, 75f);
            _act = Act.Suspect;
            _actUntil = now + 78f;
            _logger?.Info("Law: " + _dressed.Name + ": a suspect is being walked in.");
        }

        private void EndAct()
        {
            try
            {
                if (_cuffed != null)
                {
                    CuffPose.Remove(_cuffed);
                    _cuffed = null;
                    _suspect.Gait(null);
                }
            }
            catch (Exception e)
            {
                _logger?.Error(e);
            }
            _suspect.Exit();
            _mover.Exit();
            _act = Act.None;
        }

        // True if a place is within arm's reach of the sign's posts and board, which have no collider.
        private bool AtSign(Vector3 spot)
        {
            if (!_signOut)
            {
                return false;
            }
            Vector3 offset = spot - _signAt;
            float along = Mathf.Clamp(Vector3.Dot(offset, _signAcross), -0.9f, 0.9f);
            return CitySpots.Flat(offset - _signAcross * along) < 0.75f * 0.75f;
        }
    }
}
