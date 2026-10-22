using System;
using GleyTrafficSystem;
using Streets.Pedestrians;
using UI.Notification;
using UnityEngine;
using Random = UnityEngine.Random;

namespace RealLifeOverhaul
{
    /// <summary>People hit by a watched traffic car: the same roll from its real speed as for the player's, nobody charged.</summary>
    public partial class PedestrianIncidents
    {
        /// <summary>A traffic car has run into one of the game's pedestrians at <paramref name="speed"/> (m/s).</summary>
        public Strike StruckByTraffic(Pedestrian pedestrian, Vector3 carVelocity, float speed, VehicleComponent car)
        {
            try
            {
                Strike why = CanStrike(pedestrian != null ? pedestrian.tpc : null);
                if (why != Strike.Down)
                {
                    return why;
                }
                Casualty casualty = FreeCasualty();
                if (casualty == null)
                {
                    return Strike.NoRagdoll;
                }
                HornReactions.LetGo(pedestrian);
                // Somebody the mod sent across the road is the game's again from here, so the casualty takes over an
                // ordinary pedestrian.
                bool stepped = Jaywalker.Yield(pedestrian);
                if (casualty.BeginPedestrian(pedestrian, Launch(carVelocity, speed), Roll(speed), false, Time.time))
                {
                    OnTrafficHit(casualty, speed, car);
                    return Strike.Down;
                }
                if (stepped && pedestrian != null && pedestrian.isActiveAndEnabled && InstanceBehavior<CityManager>.IsInitialized)
                {
                    pedestrian.Release();   // neither thrown down nor walking anywhere: back to the pool
                }
                return Strike.Failed;
            }
            catch (Exception e)
            {
                Logger?.Error(e);
                ClearAll();
                return Strike.Failed;
            }
        }

        /// <summary>A traffic car has run into one of the game's standing figures (StreetPeople).</summary>
        public Strike StruckByTraffic(BaseHuman figure, Vector3 carVelocity, float speed, VehicleComponent car)
        {
            try
            {
                Strike why = _figuresOff ? Strike.Failed : CanStrike(figure, seatedToo: true);
                if (why != Strike.Down)
                {
                    return why;
                }
                Casualty casualty = FreeCasualty();
                if (casualty == null)
                {
                    return Strike.NoRagdoll;
                }
                HornReactions.LetGo(figure);
                if (casualty.BeginFigure(figure, Launch(carVelocity, speed), Roll(speed), false, Time.time))
                {
                    _figureFailures = 0;
                    OnTrafficHit(casualty, speed, car);
                    return Strike.Down;
                }
                FigureRefused();
                return Strike.Failed;
            }
            catch (Exception e)
            {
                Logger?.Error(e);
                ClearAll();
                return Strike.Failed;
            }
        }

        /// <summary>A traffic car has run into one of the mod's own people.</summary>
        public Strike StruckByTraffic(StreetActor actor, Vector3 carVelocity, float speed, VehicleComponent car)
        {
            try
            {
                if (actor == null || !actor.IsValid)
                {
                    return Strike.NotCounted;
                }
                Strike why = CanStrike(actor.Character);
                if (why != Strike.Down)
                {
                    return why;
                }
                Casualty casualty = FreeCasualty();
                if (casualty == null)
                {
                    return Strike.NoRagdoll;
                }
                if (casualty.BeginActor(actor, Launch(carVelocity, speed), Roll(speed), false, Time.time))
                {
                    OnTrafficHit(casualty, speed, car);
                    return Strike.Down;
                }
                return Strike.Failed;
            }
            catch (Exception e)
            {
                Logger?.Error(e);
                ClearAll();
                return Strike.Failed;
            }
        }

        /// <summary>People traffic has put down who did not get up again, this session: how a reckless driver knows it hurt somebody.</summary>
        public int TrafficHurt { get; private set; }

        /// <summary>Whether the last of those died.</summary>
        public bool TrafficKilled { get; private set; }

        private void OnTrafficHit(Casualty casualty, float speed, VehicleComponent car)
        {
            _anyone = true;
            _nextTick = Time.time + TickInterval;
            Logger?.Info($"Pedestrian hit by traffic ('{TrafficTools.Name(car)}') at {speed * 3.6f:F0} km/h: {casualty.Result}.");
            if (casualty.Result == Casualty.Outcome.Knocked)
            {
                TrafficAccidents.Notice(NotificationType.Warning, "reallife_streets_knocked", 6f, null);
                return;
            }
            TrafficHurt++;
            TrafficKilled = casualty.Dead;
        }

        // An ambulance and a patrol car, with the car that did it standing in the scene if its driver stopped. Any other
        // incident gives way.
        private void CallHelpStruck(Casualty casualty)
        {
            TrafficAccidents accidents = TrafficAccidents.Instance;
            if (accidents == null || accidents.HasCasualty)
            {
                return;   // crews are already attending someone here; this one goes with the same ambulance
            }
            StreetCrime.EndActive();
            TrafficAccidents.EndActive();
            RecklessDrivers drivers = RecklessDrivers.Instance;
            VehicleComponent car = drivers != null ? drivers.GiveUpStriker() : null;
            if (accidents.StageCasualty(casualty, casualty.Position, car, casualty.Dead, byPlayer: false, asItStands: true))
            {
                TrafficAccidents.Notice(casualty.Dead ? NotificationType.Error : NotificationType.Warning, StagedStarts[Random.Range(0, StagedStarts.Length)], 8f);
            }
            else if (car != null)
            {
                TrafficTools.ReturnToTraffic(car, "a driver who stopped for somebody they hit: no scene could start");
            }
        }
    }
}
