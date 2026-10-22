using System;
using GleyTrafficSystem;
using UnityEngine;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

namespace RealLifeOverhaul.Events
{
    /// <summary>
    /// The fire engine: a pooled truck dressed as one (<see cref="EngineLivery"/>), with lights and a siren, driven by the
    /// emergency driver the ambulance uses. It stops in its lane short of the fire, is cut out of the navigation mesh while
    /// it stands, and always leaves: driving on along the road ahead, or handed back to traffic.
    /// </summary>
    internal sealed class FireEngine
    {
        private readonly ResponseUnit _unit;
        private readonly Transform _host;
        private GameObject _lights;
        private EngineLivery _livery;

        public FireEngine(GameObject host)
        {
            _unit = new ResponseUnit(host);
            _host = host.transform;
        }

        public ResponseUnit Unit => _unit;
        public VehicleComponent Car => _unit.Car;
        public bool Out => _unit.InUse;
        public bool Lost => _unit.IsLost;
        public string Refusal { get; private set; }

        /// <summary>True while it wears the modelled body: places on it are then known (<see cref="BodyPoint"/>).</summary>
        public bool Modelled => _livery != null && _livery.Modelled != null;

        /// <summary>A place given in the modelled body's own space (metres to its right, up, and ahead of the middle of its length), in the world.</summary>
        public Vector3 BodyPoint(float right, float up, float ahead)
        {
            return _livery.Modelled.TransformPoint(new Vector3(right, up, ahead));
        }

        /// <summary>Puts the engine on the road a run-in away and sets it driving to its place in lane short of the scene.</summary>
        public bool Dispatch(SceneCordon cordon, Vector3 scene, float back)
        {
            Waypoint anchor = null;
            Vector3 stand = default;
            bool hasStand = cordon != null && cordon.Stand(false, back, out anchor, out stand);
            if (!hasStand)
            {
                anchor = TrafficTools.ClosestWaypoint(scene, 40f);
            }
            GameObject prefab = EventRoad.PickTruck(out bool van);
            VehicleComponent car = EventRoad.Bring(_unit, prefab, anchor, Random.Range(150f, 210f), 45f, out string refusal);
            if (car == null)
            {
                Refusal = refusal;
                return false;
            }
            _unit.Driver.Lit = true;   // lit wherever it drives, until its unit lets the truck go
            try
            {
                const float flash = 0.16f;
                _livery = EngineLivery.Dress(car);
                _lights = TrafficEffects.CreateFlasher(car, "RealLifeOverhaul Engine Lights", TrafficEffects.RoofHeight(car) + 0.3f,
                    new Color(1f, 0.08f, 0.06f), new Color(1f, 0.95f, 0.85f), flash);
                if (Modelled)
                {
                    // Over the body's own light bar (the truck's origin is under its rear axle), its two ends lit in turn with them.
                    _lights.transform.position = _livery.Beacon;
                    if (_livery.Lens(0) != null && _livery.Lens(1) != null)
                    {
                        var bar = _lights.AddComponent<BarFlash>();
                        bar.Left = _livery.Lens(0);     // the red light is the left one of the pair
                        bar.Right = _livery.Lens(1);
                        bar.Interval = flash;
                    }
                }
                if (Settings.Siren)
                {
                    TrafficEffects.AddSiren(_lights, car, SirenTone.Wail);
                }
            }
            catch (Exception e)
            {
                EventDirector.Logger?.Error(e);
            }
            if (hasStand)
            {
                Vector3 beyond = stand - anchor.position;
                beyond.y = 0f;
                if (beyond.sqrMagnitude > 1f)
                {
                    _unit.Path.Add(stand);
                }
                _unit.Driver.BeginRoute(car, _unit.Path, 28f);
            }
            else
            {
                _unit.Driver.Begin(car, _unit.Path, scene, back, 28f);
            }
            string wears = Modelled ? "wearing the modelled body with its axles on the truck's"
                : PropLibrary.TryGet(EngineLivery.Body, out _, out _) ? FormattableString.Invariant($"repainted (wheelbase {car.wheelDistance:F2}: not the truck the modelled body is made to)")
                : "repainted (no modelled body in the prop bundle)";
            EventDirector.Logger?.Info($"Fire: {TrafficTools.Name(car)} dispatched as the fire engine{(van ? " (a van: no truck was free)" : "")}, {wears}, {_unit.Path.Count} road points, "
                                       + (hasStand ? $"to stand {back:F0} m short of the fire in its lane." : "no lane to stand in: it pulls up where it can."));
            return true;
        }

        /// <summary>It has stopped: pinned where it stands, siren off, lights on, and people on foot go round it.</summary>
        public void Park()
        {
            _unit.Park();
            if (_unit.Car != null && !_unit.IsLost)
            {
                SceneObstacles.Add(_unit.Car, _host);
                Quiet();
            }
        }

        private void Quiet()
        {
            if (_lights != null && _lights.TryGetComponent(out AudioSource siren))
            {
                siren.Stop();
            }
        }

        /// <summary>Drives away along the road ahead, or with none is handed back to traffic where it stands.</summary>
        public void Leave()
        {
            if (_unit.InUse && !EventRoad.DriveOff(_unit, quiet: true))
            {
                Release(resumeTraffic: true, remove: false);
            }
        }

        /// <summary>Lets the truck go. The livery stays on one that drives off until the traffic pool takes it in; otherwise all of it comes off now.</summary>
        public void Release(bool resumeTraffic, bool remove)
        {
            VehicleComponent car = _unit.Car;
            if (car != null)
            {
                SceneObstacles.Remove(car);
            }
            bool drivesOff = _unit.InUse && !_unit.IsLost && resumeTraffic && !remove && TrafficTools.Ready;
            if (_lights != null)
            {
                Quiet();
                Object.Destroy(_lights, drivesOff ? 9f : 0f);
            }
            if (_livery != null && !drivesOff)
            {
                Object.Destroy(_livery.gameObject);
            }
            _lights = null;
            _livery = null;
            _unit.Release(resumeTraffic, remove, quiet: true);
        }
    }
}
