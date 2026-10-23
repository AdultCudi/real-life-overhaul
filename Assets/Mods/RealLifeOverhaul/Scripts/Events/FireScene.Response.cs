using GleyTrafficSystem;
using UI.Notification;
using UnityEngine;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

namespace RealLifeOverhaul.Events
{
    /// <summary>A fire's answer: the engine and patrol car sent and parked, the crew at work, the leaving.</summary>
    internal sealed partial class FireScene
    {
        private void SendEngine(float now)
        {
            if (_kind == FireKind.Car && !_wreckStopped)
            {
                return;
            }
            CloseRoad();
            FireEngine engine = _director.Engine;
            if (engine.Dispatch(_director.Cordon, _lanePoint, EngineBack + (_kind == FireKind.Car ? _footprints[0].HalfLength : 0f)))
            {
                _engineSent = true;
                _engineDue = now + 85f;
                _patrolAt = now + 6f;
                Notifications.Show(NotificationType.Info, _called ? "reallife_fire_engine_called" : Random.value < 0.5f ? "reallife_fire_engine_coming_a" : "reallife_fire_engine_coming_b", null, 7f);
                return;
            }
            EventDirector.Logger?.Info("Fire: no engine could be sent (" + engine.Refusal + ")" + (_engineRetried ? "; the fire is left to burn out." : "; trying once more."));
            if (!_engineRetried)
            {
                _engineRetried = true;
                _engineAt = now + 8f;
                return;
            }
            _engineSent = true;   // nothing more is tried
            Enter(Phase.BurnOut, now, _kind == FireKind.Bin ? 35f : 55f);
        }

        // The engine and the patrol car on their way, and standing.
        private void Units(float now)
        {
            FireEngine engine = _director.Engine;
            if (_engineSent && !_engineParked && engine.Out && _phase == Phase.Burning)
            {
                EmergencyDriver driver = engine.Unit.Driver;
                if (engine.Lost)
                {
                    engine.Release(true, false);
                    Enter(Phase.BurnOut, now, 45f);
                    EventDirector.Logger?.Info("Fire: the engine's truck was taken back by the traffic pool on its way; the fire is left to burn out.");
                    return;
                }
                // How far it still has to go is measured along its route, never as the crow flies: a truck on the next street is not near.
                bool waiting = driver.Active && !driver.Arrived && (driver.HeldFor > 9f || now >= _engineDue);
                float away = waiting ? driver.RouteLeft : Mathf.Sqrt(CitySpots.Flat(engine.Car.transform.position - _lanePoint));
                bool held = waiting && driver.HeldFor > 9f && away < 45f;
                if (driver.Arrived || !driver.Active || held || (now >= _engineDue && away < 60f))
                {
                    engine.Park();
                    _engineParked = true;
                    PlanCrew(engine.Car);
                    _director.Cordon.Raise(now);
                    Taper();
                    Enter(Phase.Attack, now, 0f);
                    EventDirector.Logger?.Info($"Fire: the engine is parked {away:F0} m from the fire{(held ? " (held up there by what is in front of it)" : "")}; crew out.");
                }
                else if (now >= _engineDue)
                {
                    engine.Release(true, false);
                    Enter(Phase.BurnOut, now, 40f);
                    EventDirector.Logger?.Info($"Fire: the engine was still {away:F0} m away after 85 s; it is stood down and the fire is left to burn out.");
                }
            }

            ResponseUnit patrol = _director.Patrol;
            if (_engineSent && !_patrolSent && _phase == Phase.Burning && now >= _patrolAt && Settings.Police && Settings.People && engine.Out)
            {
                _patrolSent = true;
                if (_director.Cordon.Stand(false, EngineBack + 13f + (_kind == FireKind.Car ? _footprints[0].HalfLength : 0f), out Waypoint anchor, out Vector3 stand)
                    && patrol.Spawn(UnitKind.Police, anchor, Random.Range(150f, 200f), 40f))
                {
                    Vector3 beyond = stand - anchor.position;
                    beyond.y = 0f;
                    if (beyond.sqrMagnitude > 1f)
                    {
                        patrol.Path.Add(stand);
                    }
                    patrol.Driver.BeginRoute(patrol.Car, patrol.Path, 30f);
                    EventDirector.Logger?.Info("Fire: a patrol car is sent to work the traffic.");
                }
                else
                {
                    EventDirector.Logger?.Info("Fire: no patrol car could be sent (" + (patrol.Refusal ?? "no lane to stand in") + "); the crew see to the road themselves.");
                }
            }
            if (_patrolSent && !_patrolParked && patrol.InUse && _phase != Phase.Leave)
            {
                EmergencyDriver driver = patrol.Driver;
                if (patrol.IsLost)
                {
                    patrol.Release(true, false);
                }
                else if (driver.Arrived || !driver.Active || driver.HeldFor > 8f)
                {
                    patrol.Park();
                    _patrolParked = true;
                    SceneObstacles.Add(patrol.Car, _director.transform);
                    ActorRole officer = _director.Officers[0];
                    SceneCordon cordon = _director.Cordon;
                    if (StreetSpots.BesideCar(patrol.Car, StreetSpots.Left, 0.7f, out Vector3 door) && officer.Enter(door, ActorLook.Officer, 2f, true))
                    {
                        ActorPool.PlayDoorSound(door);
                        cordon.Raise(now);
                        officer.HiVis(true);
                        officer.Go(cordon.DirectorPost, Pace.Walk, cordon.Oncoming, 25f);
                        officer.Work("direct_traffic");
                    }
                    Taper();
                }
            }
        }

        private void Taper()
        {
            SceneCordon cordon = _director.Cordon;
            float near = 0f, far = 0f;
            VehicleComponent engine = _director.Engine.Car;
            if (engine != null && _engineParked)
            {
                cordon.Reach(engine.transform.position, Mathf.Max(1.5f, engine.length * 0.5f), ref near, ref far);
            }
            VehicleComponent patrol = _director.Patrol.Car;
            if (patrol != null && _patrolParked)
            {
                cordon.Reach(patrol.transform.position, Mathf.Max(1.5f, patrol.length * 0.5f), ref near, ref far);
            }
            cordon.LayTaper(_patrolSent && !_patrolParked && _director.Patrol.InUse ? -1f : near, far);
        }

        // Where each of the three gets out, and where the nozzle and the one who keeps people back go.
        private void PlanCrew(VehicleComponent engine)
        {
            TrafficTools.Footprint(engine, out Vector3 centre, out Vector3 forward, out float halfLength, out float halfWidth);
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            float side = Vector3.Dot(_seat - centre, right) >= 0f ? 1f : -1f;
            FireEngine unit = _director.Engine;
            bool modelled = unit.Modelled;
            _panel = default;
            for (int i = 0; i < 3; i++)
            {
                Vector3 home;
                bool found;
                if (modelled)
                {
                    // On the modelled engine each gets out at a real door (the nozzle at the front, the third at the crew door)
                    // and the pump operator stands 0.50 m off the pump panel, where pump_panel's hands meet it: the engine's own
                    // place, only snapped to walkable ground.
                    float out_ = i == 1 ? EngineLivery.PanelOut + 0.50f : EngineLivery.BodyHalfWidth + 0.7f;
                    float ahead = i == 0 ? EngineLivery.FrontDoorAhead : i == 1 ? EngineLivery.PanelAhead : EngineLivery.CrewDoorAhead;
                    home = unit.BodyPoint(side * out_, 0f, ahead);
                    found = i == 1 ? StreetSpots.OnRoad(home, 0.6f, out home) : StreetSpots.OnRoad(home, 1.2f, out home) && StreetSpots.OutOfVehicles(ref home);
                    if (i == 1)
                    {
                        _panel = unit.BodyPoint(side * EngineLivery.PanelOut, 0f, EngineLivery.PanelAhead);
                    }
                }
                else
                {
                    home = centre + right * (side * (halfWidth + 0.7f)) + forward * (halfLength * 0.5f - i * 1.15f);
                    found = StreetSpots.OnRoad(home, 1.2f, out home) && StreetSpots.OutOfVehicles(ref home);
                }
                _crewHome[i] = found ? home : default;
                _crewStep[i] = 0;
            }
            // The hose starts on the ground under the outlet of that side (clear of the truck's own colliders, which are
            // wider than the modelled body), or beside a repainted truck a little behind its middle.
            _outlet = modelled
                ? unit.BodyPoint(side * Mathf.Max(EngineLivery.BodyHalfWidth, halfWidth) + side * 0.15f, 0f, EngineLivery.OutletAhead)
                : centre + right * (side * (halfWidth + 0.15f)) - forward * (halfLength * 0.2f);
            if (StreetSpots.Ground(_outlet, out float ground))
            {
                _outlet.y = ground;
            }
            StandCoil(unit, modelled, centre, forward, right, side, halfLength, halfWidth);
            // The nozzle: on the line from the engine to the fire, a few metres short of it, where there is room to stand.
            Vector3 from = centre - _seat;
            from.y = 0f;
            from = from.sqrMagnitude > 0.5f ? from.normalized : -_laneDir;
            float reach = _kind == FireKind.Bin ? 3.6f : _kind == FireKind.Car ? 5f : 5.5f;
            _attack = default;
            for (int attempt = 0; attempt < 5 && _attack == default; attempt++)
            {
                Vector3 want = _seat + Quaternion.Euler(0f, (attempt % 2 == 0 ? 1f : -1f) * (attempt / 2) * 22f, 0f) * from * (reach + (attempt / 3) * 1.5f);
                if (StreetSpots.OnRoad(want, 1.2f, out Vector3 spot) && StreetSpots.OutOfVehicles(ref spot) && CitySpots.ClearQuietly(spot, 0.4f))
                {
                    _attack = spot;
                }
            }
            Vector3 back = _seat + from * (reach + 4.5f) + Vector3.Cross(Vector3.up, from) * 3f;
            _guard = StreetSpots.OnRoad(back, 2f, out Vector3 guard) && StreetSpots.OutOfVehicles(ref guard) && CitySpots.ClearQuietly(guard, 0.4f) ? guard : default;
        }

        // Once a second: one of them gets out, the next second the next; each goes about their job.
        private void Crew(float now)
        {
            if (!_engineParked || (_phase != Phase.Attack && _phase != Phase.Overhaul && _phase != Phase.PackUp))
            {
                return;
            }
            EventPeople people = _director.People;
            CityProps shelf = people.Props.Shelf;
            VehicleComponent engine = _director.Engine.Car;
            for (int i = 0; i < 3; i++)
            {
                CityPerson who = _crew[i];
                if (who != null && who.Lost)
                {
                    if (i == 0)
                    {
                        StopWater();
                    }
                    _crew[i] = null;
                    _crewStep[i] = 9;   // knocked down by a car: the casualty system has them
                    continue;
                }
                switch (_crewStep[i])
                {
                    case 0:   // getting out (one a second, and only while the fire is being fought)
                        if (_phase == Phase.Attack && engine != null && (i == 0 || _crewStep[i - 1] != 0))
                        {
                            if (_crewHome[i] == default)
                            {
                                _crewStep[i] = 9;   // no clear ground beside the engine for this one: they stay aboard
                                break;
                            }
                            who = people.Put(CityLook.Civilian, _crewHome[i], _seat, true, false);
                            if (who == null)
                            {
                                _crewStep[i] = 9;
                                break;
                            }
                            FireOutfit.Wear(who);
                            people.Wear(who, EventProps.Helmet, HumanBodyBones.Head, 0.085f, 0.012f);
                            people.Wear(who, EventProps.AirPack, HumanBodyBones.UpperChest, 0f, 0f);
                            ActorPool.PlayDoorSound(_crewHome[i]);
                            _crew[i] = who;
                            _crewStep[i] = 1;
                            Vector3 goal = i == 0 ? _attack : i == 2 ? _guard : default;
                            if (goal != default && who.WalkTo(goal, i == 0))
                            {
                                _crewStep[i] = 2;
                            }
                            else if (i == 1)
                            {
                                // To the pump panel, or on a repainted truck its body's middle (its origin is under the rear axle).
                                Vector3 panel = _panel;
                                if (panel == default)
                                {
                                    TrafficTools.Footprint(engine, out panel, out _, out _, out _);
                                }
                                who.Turn(panel);
                                who.Work(EventPeople.Clip("pump_panel", "counter_wipe"), shelf, 0.4f);
                                _crewStep[i] = 3;
                            }
                            else
                            {
                                who.Work("radio_talk", shelf, 0.4f);     // nowhere clear to go: they stay by the engine, on the radio
                                _crewStep[i] = 3;
                            }
                            return;
                        }
                        break;
                    case 2:   // walking to their place
                        Vector3 place = i == 0 ? _attack : _guard;
                        if (who.Arrived && who.Near(place, 0.8f))
                        {
                            who.Stand();
                            _crewStep[i] = 3;
                            if (i == 0)
                            {
                                who.Turn(_seat);
                                StartWater(who, now);
                            }
                            else
                            {
                                who.Face(place + (place - _seat));   // their back to the fire, facing whoever comes too close
                                who.Work(EventPeople.Clip("keep_back", "direct_traffic"), shelf, 0.4f);
                            }
                        }
                        else if (who.Arrived)
                        {
                            who.WalkTo(place, i == 0);
                        }
                        break;
                    case 4:   // damping down: on one knee at what is left of it
                        break;
                    case 5:   // walking back to the engine
                        if ((who.Arrived && who.Near(_crewHome[i], 1.1f)) || now >= _phaseUntil - 4f)
                        {
                            ActorPool.PlayDoorSound(who.Position);
                            people.Release(who);
                            _crew[i] = null;
                            _crewStep[i] = 9;
                        }
                        else if (who.Arrived)
                        {
                            who.WalkTo(_crewHome[i]);
                        }
                        break;
                }
            }
        }

        // A spare length of hose, rolled, lies on the ground at the back of the engine on the fire's side, where nobody works
        // and the hose is not laid: stood once, as the engine parks, and given back with the hose (Depart, End).
        private void StandCoil(FireEngine unit, bool modelled, Vector3 centre, Vector3 forward, Vector3 right, float side, float halfLength, float halfWidth)
        {
            ReturnCoil();
            Vector3 want = modelled
                ? unit.BodyPoint(side * (EngineLivery.BodyHalfWidth + CoilOut), 0f, CoilAhead)
                : centre + right * (side * (halfWidth + CoilOut)) - forward * (halfLength * 0.7f);
            if (!StreetSpots.OnRoad(want, 1.2f, out Vector3 spot) || !StreetSpots.OutOfVehicles(ref spot) || !CitySpots.ClearQuietly(spot, 0.35f))
            {
                return;   // no clear ground there: the engine carries its spare hose
            }
            if (StreetSpots.Ground(spot, out float ground))
            {
                spot.y = ground;
            }
            float heading = Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg + side * CoilTurn;
            _coil = _director.Props.Stand(EventProps.HoseCoil, spot, heading);
        }

        private void ReturnCoil()
        {
            if (_coil != null)
            {
                _director.Props.Return(_coil);
                _coil = null;
            }
        }

        // The nozzle is in their hands, the hose behind them, water on the fire.
        private void StartWater(CityPerson who, float now)
        {
            EventPeople people = _director.People;
            _nozzle = people.Props.Take(EventProps.Nozzle);
            if (_nozzle == null)
            {
                return;
            }
            Transform body = who.Character.transform;
            Vector3 at = body.position + body.forward * 0.46f + body.right * 0.17f + Vector3.up * 1.02f;
            _nozzle.SetParent(_director.transform, true);
            _nozzle.position = at;
            Aim(_seat);
            if (ClipLibrary.Has("hose_hold") && ClipLibrary.TryGet("hose_hold", out CityClip hold))
            {
                // The clip is made to this nozzle with the palms on its two grips: the wrists are kept where its grip has them.
                who.Work("hose_hold", people.Props.Shelf, 0.3f, ClipEnd.Return, false);
                _director.Hold.Begin(who.Character.animator, _nozzle, hold.Grip);
            }
            else
            {
                who.WorkOver("carry_box_hold", people.Props.Shelf, 0.3f, ClipEnd.Return, BodyMask.Arms, false);   // both arms forward; the hands are then put on the nozzle
                _director.Hold.Begin(who.Character.animator, _nozzle);
            }
            float run = Mathf.Sqrt(CitySpots.Flat(_outlet - at));
            if (run > 34f || !_director.Hose.Lay(_outlet, at - _nozzle.forward * 0.05f))
            {
                EventDirector.Logger?.Info($"Fire: no hose laid ({run:F0} m from the engine to the nozzle).");
            }
            _waterOn = true;
            _waterAt = now;
            _stage = 0;
            _phaseUntil = now + 8f;
            EventDirector.Logger?.Info($"Fire: water on, {now - _startedAt:F0} s after the first smoke.");
        }

        private void Aim(Vector3 target)
        {
            if (_nozzle == null)
            {
                return;
            }
            Vector3 line = target - _nozzle.position;
            Vector3 flat = new Vector3(line.x, 0f, line.z);
            // Held a little above the line to the fire: the water arcs on to it.
            Vector3 aim = flat.sqrMagnitude > 0.01f ? (flat.normalized + Vector3.up * Mathf.Clamp(line.y / Mathf.Max(1f, flat.magnitude) + 0.22f, 0.05f, 0.9f)).normalized : Vector3.forward;
            _nozzle.rotation = Quaternion.LookRotation(aim);
            _director.Effects.Jet(_nozzle.position + _nozzle.forward * 0.42f, target, true);
        }

        private void StopWater()
        {
            _director.Effects.Jet(default, default, false);
            _director.Hold.End();
            if (_nozzle != null)
            {
                _director.People.Props.Return(_nozzle);
                _nozzle = null;
            }
        }

        // The fire is knocked down in three stages, then it is out.
        private void Knockdown(float now)
        {
            if (now < _phaseUntil)
            {
                return;
            }
            FireEffects effects = _director.Effects;
            _stage++;
            if (_stage == 1)
            {
                effects.SetFlame(0, 0.6f);
                effects.SetFlame(1, 0.5f);
                effects.Glow(_seat + Vector3.up * 1.2f, 0.7f);
                _phaseUntil = now + 8f;
            }
            else if (_stage == 2)
            {
                effects.SetFlame(0, 0.25f);
                effects.SetFlame(1, 0f);
                effects.Smoke(0, _seat + Vector3.up * 0.6f, true, 12f);
                effects.Steam(_seat + Vector3.up * 0.3f, true);
                effects.Glow(_seat + Vector3.up * 1.2f, 0.35f);
                if (_hasUpper)
                {
                    Aim(_upper);   // the ground floor is under control: up to the window
                }
                _phaseUntil = now + 8f;
            }
            else
            {
                for (int i = 0; i < FireEffects.Flames; i++)
                {
                    effects.SetFlame(i, 0f);
                }
                effects.Smoke(0, _seat + Vector3.up * 0.6f, true, 6f);
                if (_hasUpper)
                {
                    effects.Smoke(1, _upper + Vector3.up * 0.8f, true, 6f);
                }
                effects.Glow(default, 0f);
                StopWater();
                Enter(Phase.Overhaul, now, 11f);
                Notifications.Show(NotificationType.Success, Random.value < 0.5f ? "reallife_fire_out_a" : "reallife_fire_out_b", null, 8f);
                // Damping down: the one who had the nozzle goes in close and down on a knee at what is left.
                CityPerson lead = _crew[0];
                if (lead != null && !lead.Lost)
                {
                    lead.StopOver(0.3f);
                    lead.Work("kneel_work", _director.People.Props.Shelf, 0.5f);
                    _crewStep[0] = 4;
                }
                EventDirector.Logger?.Info($"Fire: out {now - _startedAt:F0} s after the first smoke ({now - _waterAt:F0} s of water).");
            }
        }

        private void SendCrewHome()
        {
            StopWater();
            for (int i = 0; i < 3; i++)
            {
                CityPerson who = _crew[i];
                if (who == null || who.Lost)
                {
                    continue;
                }
                if (_crewHome[i] != default && who.WalkTo(_crewHome[i]))
                {
                    _crewStep[i] = 5;
                }
                else
                {
                    _director.People.Release(who);
                    _crew[i] = null;
                    _crewStep[i] = 9;
                }
            }
        }

        // The show is over: the bystanders and the car's driver walk off, out of view, before they are taken back.
        private void SendWatchersOff()
        {
            for (int i = 0; i < Bystanders; i++)
            {
                _director.People.SendOff(_watchers[i]);
                _watchers[i] = null;
            }
            _director.People.SendOff(_driver);
            _driver = null;
        }

        private bool CrewAboard()
        {
            return _crew[0] == null && _crew[1] == null && _crew[2] == null;
        }

        // What the fire cost, once it is out or has burned out: only for a business of the player's own.
        private void Settle()
        {
            if (_kind != FireKind.Building || _business == null)
            {
                return;
            }
            BuildingRegistration business = _business;
            _business = null;
            FireDamage.Apply(business, _waterOn ? _waterAt - _startedAt : 120f, _warned, _called);
        }

        // Tape and cones in; the burnt car goes; the vehicles leave.
        private void Depart(float now)
        {
            StopWater();
            _director.Hose.Take();
            ReturnCoil();
            for (int i = 0; i < 3; i++)
            {
                if (_crew[i] != null)
                {
                    _director.People.Release(_crew[i]);
                    _crew[i] = null;
                }
            }
            SceneCordon cordon = _director.Cordon;
            cordon.LiftTaper();
            cordon.Lower();
            ActorRole officer = _director.Officers[0];
            VehicleComponent patrol = _director.Patrol.Car;
            if (officer.Present && patrol != null && _patrolParked && StreetSpots.BesideCar(patrol, StreetSpots.Left, 0.7f, out Vector3 door))
            {
                officer.HiVis(false);
                officer.Board(door, Pace.Walk, 14f);
            }
            else
            {
                officer.Exit();
            }
            Enter(Phase.Leave, now, 45f);
            _leaveAt = now + 5f;
            _stage = 0;
        }

        // The leaving, a step a second: the wreck is taken away, the road opened, the engine and then the patrol car drive off.
        private bool Left(float now)
        {
            FireEngine engine = _director.Engine;
            ResponseUnit patrol = _director.Patrol;
            ResponseUnit wreck = _director.Wreck;
            if (_stage == 0)
            {
                bool unseen = !wreck.InUse || wreck.Car == null || !CitySpots.InView(wreck.Car.transform.position);
                if (now < _leaveAt || (!unseen && now < _leaveAt + 14f))
                {
                    return false;
                }
                ReleaseWreck(true);
                _director.Effects.StopAll();
                _director.Cordon.Open();
                if (engine.Out && _engineParked)
                {
                    engine.Leave();
                }
                else if (engine.Out)
                {
                    engine.Release(true, false);
                }
                _stage = 1;
                _leaveAt = now + 5f;
                EventDirector.Logger?.Info("Fire: the road is open again; the engine is leaving.");
                return false;
            }
            if (_stage == 1 && now >= _leaveAt && !_director.Officers[0].Present)   // the officer is aboard (Board has its own time limit)
            {
                _stage = 2;
                if (patrol.InUse && (!_patrolParked || !EventRoad.DriveOff(patrol, true)))
                {
                    patrol.Release(true, false, true);
                }
            }
            // Gone once they have driven their route out, or after the time allowed: then they are ordinary traffic again.
            bool engineGone = !engine.Out || engine.Lost || engine.Unit.Driver.Arrived || !engine.Unit.Driver.Active;
            bool patrolGone = _stage >= 2 && (!patrol.InUse || patrol.IsLost || patrol.Driver.Arrived || !patrol.Driver.Active);
            return (engineGone && patrolGone) || now >= _phaseUntil;
        }

        private void ReleaseWreck(bool trafficRunning)
        {
            if (_hazards != null)
            {
                Object.Destroy(_hazards);
                _hazards = null;
            }
            ResponseUnit wreck = _director.Wreck;
            if (wreck.InUse)
            {
                if (wreck.Car != null)
                {
                    SceneObstacles.Remove(wreck.Car);
                }
                wreck.Release(trafficRunning, remove: true);   // a burnt-out car does not drive away: it is taken off the road
            }
        }
    }
}
