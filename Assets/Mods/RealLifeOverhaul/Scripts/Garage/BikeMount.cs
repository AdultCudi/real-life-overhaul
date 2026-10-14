using System;
using System.Collections.Generic;
using BAModAPI;
using Helpers;
using NWH.Common.Vehicles;
using NWH.VehiclePhysics2.Modules.SpeedLimiter;
using NWH.VehiclePhysics2.Powertrain;
using NWH.VehiclePhysics2.Sound.SoundComponents;
using UnityEngine;
using NwhVehicle = NWH.VehiclePhysics2.VehicleController;

namespace RealLifeOverhaul
{
    /// <summary>
    /// One of the game's electric scooters turned into a bicycle or a motorbike. Everything that makes a
    /// scooter rideable stays the game's: its controller, its two-wheel physics (the lean, the steering),
    /// getting on and off, the hands on the bars, the camera, the save. This component gives it another
    /// body (the scooter's own renderers are switched to not draw, never removed), other numbers (a
    /// bicycle's small "engine" behind the pedals with air drag that sets its cruising speed; a
    /// motorbike's power, gearing and drag from the same model the cars use), and a rider who sits it:
    /// a riding clip of the mod's own chosen by what they are doing (pedalling, freewheeling, stopped with
    /// a foot down; riding or stopped on a motorbike), or without the clip bundle the game's riding pose
    /// moved to the saddle; a bicycle's feet are put on the pedals after the animation.
    /// Every value changed is recorded and <see cref="Detach"/> puts the scooter back exactly.
    /// Costs nothing while parked: the per-frame and per-step work runs only while it is ridden.
    /// </summary>
    [DefaultExecutionOrder(5000)]   // after the game has posed the rider, before the mod redraws the player (Character/PlayerDouble)
    public sealed class BikeMount : MonoBehaviour
    {
        private static readonly FeatureGuard FrameGuard = Guard.For("garage", "BikeMount", typeof(BikeMount));   // its Update, LateUpdate and FixedUpdate (Core/Guard)

        private const float CadencePerMetre = 1.41f;   // radians of crank per metre: 75 turns a minute at 20 km/h
        private const float AnkleAbovePedal = 0.10f;
        private const float BicycleRolling = 6f;       // N m per wheel: a bicycle coasts; the scooter's own tyres stop it in a few metres
        private const float PushOff = 3.2f;            // m/s2 of extra push from a standstill while pedalling (about a third of a g: a hard push on the pedals)
        private const float PushOffUntil = 6f;         // m/s by which that push has faded to nothing (about 22 km/h); pedal power carries on from there
        private const float StopBelow = 0.35f;         // m/s: slower than this with no throttle is standing still (a foot goes down)
        private const float MoveAbove = 0.8f;          // m/s: and faster than this is riding again
        private const float PoseFade = 0.35f;          // seconds from one riding clip to the next
        private const float LegsFade = 0.3f;           // seconds for the feet to leave the pedals for the clip's own legs, and back
        private const float CrankHome = 5f;            // radians a second: the cranks brought round to "right pedal forward" when stopped

        private enum Riding { Ride, Coast, Stand }

        public static IModLogger Logger;

        private static readonly CarSetup Setup = new CarSetup();
        private static readonly HashSet<int> Logged = new HashSet<int>();

        private ScooterController _scooter;
        private NwhVehicle _nwh;
        private Rigidbody _rigidbody;
        private BikeBody _body;
        private readonly List<Renderer> _hidden = new List<Renderer>(8);
        private PointOfInterest _poi;   // the game's own vehicle marker, which it does not give a scooter

        // what the scooter was like
        private bool _changed;
        private float _power, _loss, _final, _brake, _limit;
        private float[] _gears;
        private float[] _rolling;
        private SpeedLimiterModule _limiter;
        private SoundComponent[] _sounds;
        private float[] _volumes;
        private float _noteRange;
        private AutoDestroyVehicle _autoDestroy;
        private bool _autoDestroyWas;
        private EngineComponent.PowerModifier _effortModifier;

        // while ridden
        private bool _ridden;
        private float _drag;
        private float _effort = 1f;
        private float _spin, _crank;
        private Vector3 _lastVelocity;
        private Vector3 _lastPosition;
        private Animator _animator;
        private Transform _player;
        private Transform _head;
        private Transform _thighL, _shinL, _footL, _thighR, _shinR, _footR;
        private GameObject _helmet;
        private Quaternion _headSquare = Quaternion.identity;   // from the Head bone's own axes to the character's (helmet: visor +Z, crown +Y)
        private bool _headKnown;
        private WheelUAPI _front;
        private ActivityAnimator _seated;   // the mod's own riding clip on the rider, when the clip bundle has one
        private AnimationClip _rideClip, _coastClip, _standClip;   // by what the rider is doing; coast and stand may be missing
        private Riding _riding;
        private float _otherFor;            // seconds the rider has been doing something else than the clip shows
        private float _legs = 1f;           // how much of a cyclist's legs is the pedals' (1) and how much the clip's (0: a foot on the ground)

        public BikeRecord Record { get; private set; }

        public BikeDef Def { get; private set; }

        public ScooterController Scooter => _scooter;

        public bool Ridden => _ridden;

        /// <summary>Set when the rider has come off (the speed they were doing, m/s); the director deals with it and clears it.</summary>
        public float Fell { get; set; }

        /// <summary>Signed speed along the bike, m/s.</summary>
        public float Speed => _nwh != null ? _nwh.Speed : 0f;

        public bool Pedalling => _ridden && _nwh != null && _nwh.input != null && _nwh.input.Throttle > 0.1f && Mathf.Abs(_nwh.Speed) > 0.5f;

        /// <summary>How hard the rider can push: 1 fresh, 0.5 worn out. Bicycles only.</summary>
        public float Effort
        {
            get => _effort;
            set => _effort = value;
        }

        public static BikeMount Attach(ScooterController scooter, BikeRecord record)
        {
            if (scooter == null || record == null || scooter.GetComponent<BikeMount>() != null)
            {
                return null;
            }
            var mount = scooter.gameObject.AddComponent<BikeMount>();
            mount.enabled = false;
            mount._scooter = scooter;
            mount.Record = record;
            mount.Def = BikeCatalogue.Of(record.Kind);
            try
            {
                mount.Dress();
                mount.Tune();
            }
            catch (Exception e)
            {
                Logger?.Warn("Two-wheelers: a " + record.Kind + " could not be made of this scooter; it stays a scooter. " + e);
                mount.Detach();
                return null;
            }
            mount.Mark();
            return mount;
        }

        // ---------------------------------------------------------------- the body

        private void Dress()
        {
            // The scooter's own body: not drawn, and otherwise left as it is (the game still asks its renderers things).
            // Taken before our body exists, so nothing of ours can ever be among them: the renderers the scooter
            // itself lists, and every mesh under it that list leaves out (a wheel, the bag it carries).
            Hide(_scooter.Renderers);
            GameInstance save = SaveGameManager.Current;
            bool ridden = _scooter.controlledByPlayer || (save != null && _scooter.vehicleInstance != null && save.ActiveVehicleId == _scooter.vehicleInstance.id);
            if (!ridden)   // with somebody on it (a save made in the saddle) the rider is among its children: only the scooter's own list then
            {
                Hide(_scooter.GetComponentsInChildren<MeshRenderer>(true));
                Hide(_scooter.GetComponentsInChildren<SkinnedMeshRenderer>(true));
            }

            _body = BikeBody.Build(Def, transform, Record.Colour);
            if (_body == null)
            {
                throw new InvalidOperationException("the game's lit shader was not found");
            }
            // And the other way round: whatever the scooter does to its children, ours are drawn.
            Renderer[] ours = _body.Root.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < ours.Length; i++)
            {
                ours[i].forceRenderingOff = false;
                ours[i].enabled = true;
            }
        }

        private void Hide(Renderer[] own)
        {
            for (int i = 0; own != null && i < own.Length; i++)
            {
                if (own[i] != null && !own[i].forceRenderingOff)
                {
                    own[i].forceRenderingOff = true;
                    _hidden.Add(own[i]);
                }
            }
        }

        // The game marks the player's cars on the map and, within 100 m, on the street; it leaves scooters out
        // (they are hire scooters to it). Ours get that same marker, under their own name, so that they can be found.
        private void Mark()
        {
            try
            {
                if (_scooter.poi != null)
                {
                    return;
                }
                var city = InstanceBehavior<CityManager>.Instance;
                var references = InstanceBehavior<GlobalReferences>.Instance;
                if (city == null || city.cityMap == null || references == null)
                {
                    return;
                }
                PointOfInterest poi = city.cityMap.AddPoi(transform, references.vehiclePOIIcon, references.vehiclePOIBackgroundColor, GarageText.T(Def.Name));
                if (poi == null)
                {
                    return;
                }
                poi.offset = new Vector3(0f, 0.3f, 0f);   // what the game gives a scooter's marker
                poi.SetPermanent();
                if (_scooter.controlledByPlayer)
                {
                    poi.SetHidden(true);   // as the game hides a vehicle's marker while it is driven
                }
                _scooter.poi = poi;
                _poi = poi;
            }
            catch (Exception e)
            {
                Logger?.Warn("Two-wheelers: the map marker of a " + Def.Kind + " could not be made (it is still there, unmarked). " + e.Message);
            }
        }

        public bool Marked => _poi != null;

        /// <summary>
        /// Everything about where this two-wheeler is and whether it can be seen, as one line for the log: its
        /// place, the ground found under it, what of its body is drawn and where that is, what of the scooter is
        /// hidden, and how the game holds it. Builds a string: called when one is made and once shortly after.
        /// </summary>
        public string Report()
        {
            Vector3 at = transform.position;
            bool ground = BikePlaces.StandHeight(at, out float height, out bool pavement);
            int drawn = 0, total = 0, forcedOff = 0, noMaterial = 0;
            var bounds = new Bounds(at, Vector3.zero);
            bool any = false;
            if (_body != null && _body.Root != null)
            {
                Renderer[] ours = _body.Root.GetComponentsInChildren<Renderer>(true);
                total = ours.Length;
                for (int i = 0; i < ours.Length; i++)
                {
                    Renderer renderer = ours[i];
                    if (renderer.forceRenderingOff)
                    {
                        forcedOff++;
                    }
                    if (renderer.sharedMaterial == null)
                    {
                        noMaterial++;
                    }
                    if (renderer.enabled && renderer.gameObject.activeInHierarchy && !renderer.forceRenderingOff)
                    {
                        drawn++;
                        if (any)
                        {
                            bounds.Encapsulate(renderer.bounds);
                        }
                        else
                        {
                            bounds = renderer.bounds;
                            any = true;
                        }
                    }
                }
            }
            int stillDrawn = 0;
            Renderer[] all = GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < all.Length; i++)
            {
                if ((all[i] is MeshRenderer || all[i] is SkinnedMeshRenderer) && !all[i].forceRenderingOff && all[i].enabled
                    && (_body == null || _body.Root == null || !all[i].transform.IsChildOf(_body.Root.transform)))
                {
                    stillDrawn++;
                }
            }
            Collider collider = _scooter.vehicleCollider;
            Vector3 scale = transform.lossyScale;
            return $"{Def.Kind} at {at.ToString("F2")}, heading {transform.eulerAngles.y:F0}, {Vector3.Angle(transform.up, Vector3.up):F0} deg off upright; "
                   + (ground ? $"ground under it at {height:F2} ({(pavement ? "pavement" : "not pavement")}), so it stands {at.y - height:F2} above it; " : "NO GROUND found under it; ")
                   + $"body: {(_body == null ? "none" : _body.Modelled ? "modelled" : "stand-in")}, {drawn} of {total} renderers drawn, {forcedOff} forced off, {noMaterial} without a material, "
                   + (any ? $"bounds centre {bounds.center.ToString("F2")} size {bounds.size.ToString("F2")}; " : "no bounds; ")
                   + $"scooter's own: {_hidden.Count} hidden, {stillDrawn} still drawn; object {(gameObject.activeInHierarchy ? "active" : "INACTIVE")}, layer {LayerMask.LayerToName(gameObject.layer)}, "
                   + $"scale {scale.ToString("F2")}, collider {(collider != null ? "on " + LayerMask.LayerToName(collider.gameObject.layer) : "none")}, "
                   + (_rigidbody != null ? $"body {(_rigidbody.isKinematic ? "kinematic" : "dynamic")} {_rigidbody.constraints}, " : "no rigidbody, ")
                   + $"physics {(_nwh != null && _nwh.enabled ? "running" : "at rest")}, map marker {(_poi != null ? "yes" : "no")}.";
        }

        // ---------------------------------------------------------------- the numbers

        private void Tune()
        {
            _nwh = _scooter.GetComponent<NwhVehicle>();
            _rigidbody = _nwh != null ? _nwh.vehicleRigidbody : null;
            if (_nwh == null || _rigidbody == null || _nwh.powertrain == null || _nwh.powertrain.engine == null
                || _nwh.powertrain.transmission == null || _nwh.powertrain.wheels == null || _nwh.powertrain.wheels.Count < 2 || _nwh.brakes == null)
            {
                throw new InvalidOperationException("the scooter's drivetrain is not what was expected");
            }
            EngineComponent engine = _nwh.powertrain.engine;
            TransmissionComponent box = _nwh.powertrain.transmission;
            var limiter = _scooter.GetComponent<SpeedLimiterModuleWrapper>();

            // Record first.
            _power = engine.maxPower;
            _loss = engine.engineLossPercent;
            _final = box.finalGearRatio;
            _gears = box.gears.ToArray();
            _brake = _nwh.brakes.maxTorque;
            _limiter = limiter != null ? limiter.module : null;
            _limit = _limiter != null ? _limiter.speedLimit : 0f;
            _rolling = new float[_nwh.powertrain.wheels.Count];
            float wheelBase = 0f;
            for (int i = 0; i < _rolling.Length; i++)
            {
                WheelUAPI wheel = _nwh.powertrain.wheels[i].wheelUAPI;
                _rolling[i] = wheel != null ? wheel.RollingResistanceTorque : 0f;
                if (wheel != null)
                {
                    float z = transform.InverseTransformPoint(wheel.transform.position).z;
                    wheelBase = Mathf.Max(wheelBase, Mathf.Abs(z));
                    if (_front == null || z > transform.InverseTransformPoint(_front.transform.position).z)
                    {
                        _front = wheel;
                    }
                }
            }
            _autoDestroy = _scooter.GetComponent<AutoDestroyVehicle>();
            _autoDestroyWas = _autoDestroy != null && _autoDestroy.enabled;
            _changed = true;

            // The game takes a scooter away four hours after it was last looked at. These are kept: an owned one is
            // the player's, a hired one is collected by the mod (TwoWheelers), which also tells the player.
            if (_autoDestroy != null)
            {
                _autoDestroy.enabled = false;
            }

            string line;
            if (Def.Bicycle)
            {
                var sound = _nwh.soundManager;
                if (sound != null)
                {
                    _sounds = new SoundComponent[]
                    {
                        sound.engineRunningComponent, sound.engineStartStopComponent, sound.engineFanComponent, sound.exhaustPopComponent,
                        sound.gearChangeComponent, sound.transmissionWhineComponent
                    };
                    _volumes = new float[_sounds.Length];
                    for (int i = 0; i < _sounds.Length; i++)
                    {
                        if (_sounds[i] != null)
                        {
                            _volumes[i] = _sounds[i].baseVolume;
                            _sounds[i].baseVolume = 0f;   // nobody's bicycle hums
                            if (_sounds[i].source != null)
                            {
                                _sounds[i].source.volume = 0f;
                            }
                        }
                    }
                    _noteRange = sound.engineRunningComponent.volumeRange;
                    sound.engineRunningComponent.volumeRange = 0f;
                }
                engine.maxPower = Def.PowerKw;
                engine.engineLossPercent = 0.03f;   // lift off and it rolls on
                engine.UpdatePeakPowerAndTorque();
                float rolling = 0f;
                float radius = 0.33f;
                for (int i = 0; i < _rolling.Length; i++)
                {
                    WheelUAPI wheel = _nwh.powertrain.wheels[i].wheelUAPI;
                    if (wheel != null && wheel.Radius > 0.05f)
                    {
                        wheel.RollingResistanceTorque = BicycleRolling;
                        rolling += BicycleRolling / wheel.Radius;
                        radius = wheel.Radius;
                    }
                }
                // Cruising speed is where the legs' power meets the air: drag is whatever is left of the push at that speed.
                float cruise = Def.CruiseKmh / 3.6f;
                float ratio = Mathf.Abs(box.gears[box.gears.Count - 1] * box.finalGearRatio);
                float revs = cruise / radius * ratio / 0.10471976f / Mathf.Max(1f, engine.revLimiterRPM);
                float share = engine.powerCurve != null ? Mathf.Clamp01(engine.powerCurve.Evaluate(Mathf.Clamp01(revs))) : 0.5f;
                float push = Def.PowerKw * share * 1000f / cruise - rolling;
                _drag = push > 0f ? push / (cruise * cruise) : 0f;
                _effortModifier = EffortFactor;
                engine.powerModifiers.Add(_effortModifier);
                line = $"Two-wheelers: {Def.Kind}: {_power:F0} -> {Def.PowerKw:F1} kW behind the pedals, cruising {Def.CruiseKmh:F0} km/h "
                       + $"(engine at {revs * 100f:F0} % of its revs gives {share * 100f:F0} % of that; drag {_drag:F2}, rolling {rolling:F0} N), {_rigidbody.mass:F0} kg, engine sounds off.";
            }
            else if (GarageTuning.SolveBike(_nwh, Def.Seconds, Def.TopKmh, Setup, out CarRun run, out int firstForward, out string why)
                     && Setup.PowerKw >= 5f && Setup.PowerKw <= _rigidbody.mass)
            {
                float final = Setup.Ratios[Setup.Ratios.Length - 1] / box.gears[box.gears.Count - 1];
                if (!(final > 0.2f) || !(final < 60f) || Setup.Ratios.Length != box.gears.Count - firstForward)
                {
                    throw new InvalidOperationException("the sums came out at a final drive of " + final);
                }
                engine.maxPower = Setup.PowerKw;
                engine.UpdatePeakPowerAndTorque();
                for (int i = 0; i < box.gears.Count; i++)
                {
                    box.gears[i] = i >= firstForward ? Setup.Ratios[i - firstForward] / final : _gears[i] * _final / final;
                }
                box.finalGearRatio = final;
                _drag = Setup.Drag;
                if (_limiter != null)
                {
                    _limiter.speedLimit = 0f;   // the limiter's own "no limit": drag ends the speed now
                }
                _nwh.brakes.maxTorque = _brake * 1.6f;   // it has three times the scooter's speed to lose
                line = $"Two-wheelers: {Def.Kind}: target 0-100 {Def.Seconds:F1} s, top {Def.TopKmh:F0} km/h; power {_power:F0} -> {Setup.PowerKw:F0} kW, "
                       + $"final drive {_final:F2} -> {final:F2}, drag {_drag:F3}, limiter {_limit:F0} -> off, brakes x1.6; "
                       + $"expect 0-50 {run.To50:F1} s, 0-100 {run.To100:F1} s, top {run.TopKmh:F0} km/h" + (Setup.TyreLimited ? " (the tyre cannot put down what the time needs)" : string.Empty) + ".";
            }
            else
            {
                line = "Two-wheelers: " + Def.Kind + " keeps the scooter's own performance (the model cannot drive it).";
            }
            if (Mathf.Abs(wheelBase - BikeCatalogue.ScooterFrontZ) > 0.12f)
            {
                line += " The scooter's wheels are not where the bodies were made for (front at " + wheelBase.ToString("F2") + " m).";
            }
            if (Logged.Add((int)Def.Kind))
            {
                Logger?.Info(line);
            }
        }

        private float EffortFactor()
        {
            return _effort;
        }

        /// <summary>The scooter exactly as the game made it, and this component gone.</summary>
        public void Detach()
        {
            if (_ridden)
            {
                Dismounted();
            }
            try
            {
                for (int i = 0; i < _hidden.Count; i++)
                {
                    if (_hidden[i] != null)
                    {
                        _hidden[i].forceRenderingOff = false;
                    }
                }
                _hidden.Clear();
                if (_poi != null)
                {
                    if (_scooter != null && ReferenceEquals(_scooter.poi, _poi))
                    {
                        _scooter.poi = null;
                    }
                    Destroy(_poi.gameObject);
                    _poi = null;
                }
                if (_body != null && _body.Root != null)
                {
                    Destroy(_body.Root);
                }
                _body = null;
                if (_changed && _nwh != null && _nwh.powertrain != null)
                {
                    _changed = false;
                    EngineComponent engine = _nwh.powertrain.engine;
                    TransmissionComponent box = _nwh.powertrain.transmission;
                    if (_effortModifier != null)
                    {
                        engine.powerModifiers.Remove(_effortModifier);
                    }
                    engine.maxPower = _power;
                    engine.engineLossPercent = _loss;
                    engine.UpdatePeakPowerAndTorque();
                    box.finalGearRatio = _final;
                    for (int i = 0; i < _gears.Length && i < box.gears.Count; i++)
                    {
                        box.gears[i] = _gears[i];
                    }
                    _nwh.brakes.maxTorque = _brake;
                    if (_limiter != null)
                    {
                        _limiter.speedLimit = _limit;
                    }
                    for (int i = 0; i < _rolling.Length && i < _nwh.powertrain.wheels.Count; i++)
                    {
                        WheelUAPI wheel = _nwh.powertrain.wheels[i].wheelUAPI;
                        if (wheel != null)
                        {
                            wheel.RollingResistanceTorque = _rolling[i];
                        }
                    }
                    if (_sounds != null)
                    {
                        for (int i = 0; i < _sounds.Length; i++)
                        {
                            if (_sounds[i] != null)
                            {
                                _sounds[i].baseVolume = _volumes[i];
                            }
                        }
                        _nwh.soundManager.engineRunningComponent.volumeRange = _noteRange;
                        _sounds = null;
                    }
                    if (_autoDestroy != null)
                    {
                        _autoDestroy.enabled = _autoDestroyWas;
                    }
                }
            }
            catch (Exception e)
            {
                Logger?.Warn("Two-wheelers: a scooter could not be put back completely. " + e);
            }
            Destroy(this);
        }

        // ---------------------------------------------------------------- the rider

        /// <summary>The player has just got on (the game has attached them and given them its riding pose).</summary>
        public void Mounted(PlayerController player)
        {
            if (_ridden || player == null || _body == null)
            {
                return;
            }
            _ridden = true;
            Fell = 0f;
            _player = player.transform;
            _lastVelocity = _rigidbody != null ? _rigidbody.velocity : Vector3.zero;
            _lastPosition = transform.position;
            _animator = PlayerHelper.GetAnimator();
            float hip = 0.95f;
            if (_animator != null && _animator.isHuman)
            {
                Transform hips = _animator.GetBoneTransform(HumanBodyBones.Hips);
                _head = _animator.GetBoneTransform(HumanBodyBones.Head);
                _thighL = _animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
                _shinL = _animator.GetBoneTransform(HumanBodyBones.LeftLowerLeg);
                _footL = _animator.GetBoneTransform(HumanBodyBones.LeftFoot);
                _thighR = _animator.GetBoneTransform(HumanBodyBones.RightUpperLeg);
                _shinR = _animator.GetBoneTransform(HumanBodyBones.RightLowerLeg);
                _footR = _animator.GetBoneTransform(HumanBodyBones.RightFoot);
                if (hips != null)
                {
                    // How high this character's hips are when standing: what the saddle is fitted to.
                    hip = Mathf.Clamp(Vector3.Dot(hips.position - _player.position, _player.up), 0.75f, 1.15f);
                }
            }
            // Riding clips of the mod's own, when the clip bundle has them: seated loops made with the feet's ground
            // under the saddle and the palms on this model's grips (CityAssets/README.md, "Garage"). One for riding
            // (pedalling, on a bicycle), one for freewheeling (bicycles), one for standing still with a foot down;
            // each model's own, else the town bicycle's or the roadster's. Without them the game's own riding pose.
            _seated = null;
            _rideClip = Loop(Def.Ride, Def.Motor ? "moto_ride" : "cycle_ride");
            _coastClip = Def.Bicycle ? Loop(Def.Coast, "cycle_coast") : null;
            _standClip = Loop(Def.Stand, Def.Motor ? "moto_stand" : "cycle_stand");
            _riding = _standClip != null && _nwh != null && Mathf.Abs(_nwh.Speed) < StopBelow ? Riding.Stand : Riding.Ride;
            _otherFor = 0f;
            _legs = _riding == Riding.Stand ? 0f : 1f;
            if (_animator != null && _rideClip != null && ActivityAnimators.Working)
            {
                ActivityAnimator seated = ActivityAnimators.For(_animator);
                if (seated != null && seated.Usable && seated.Play(_riding == Riding.Stand ? _standClip : _rideClip, 0.25f))
                {
                    _seated = seated;
                }
            }
            if (_seated == null)
            {
                _riding = Riding.Ride;
                _legs = 1f;
            }
            // The game seats the rider at the scooter's origin, standing on its deck. Moved over the seat, and (in the
            // game's standing pose) up or down so that the hip joint is just above it: the joint is some 8 cm above
            // what one sits on. A riding clip brings its own height.
            float lift = _seated != null ? 0f : Mathf.Clamp(Def.Seat.x + 0.08f - hip, -0.3f, 0.3f);
            _player.localPosition = new Vector3(0f, lift, Def.Seat.y + 0.04f);

            // The hands: the game's own hand rig, aimed at our grips instead of the scooter's bar. The grips
            // take the scooter's own grip rotation, since that is what the rig was set up to hold.
            if (_scooter.LHandIKAttachmentPoint != null && _scooter.RHandIKAttachmentPoint != null && player.Character != null)
            {
                _body.HandLeft.rotation = _scooter.LHandIKAttachmentPoint.rotation;
                _body.HandRight.rotation = _scooter.RHandIKAttachmentPoint.rotation;
                player.Character.SetHandIKTargets(_body.HandLeft, _body.HandRight);
            }
            if (Def.Motor && GarageState.Helmet && _head != null)
            {
                _helmet = BikeBody.Helmet(transform);
                _headKnown = _helmet != null && HeadSquare(_animator, _head, out _headSquare);
            }
            enabled = true;
        }

        // The Head bone's axes are the rig's, not the character's (other features measure them too: ServiceKit,
        // EventPeople.Wear). In the avatar's T-pose the head is held straight, so the bone's rotation there,
        // relative to the character, is what stands between the two: read once per mount from the avatar's own
        // skeleton. False when the avatar does not say (the helmet then keeps the player's heading, as it used to).
        private static bool HeadSquare(Animator animator, Transform head, out Quaternion square)
        {
            square = Quaternion.identity;
            try
            {
                SkeletonBone[] skeleton = animator != null && animator.avatar != null ? animator.avatar.humanDescription.skeleton : null;
                if (skeleton == null || skeleton.Length == 0)
                {
                    return false;
                }
                Quaternion inPose = Quaternion.identity;   // the Head bone's rotation relative to the character, in the T-pose
                bool found = false;
                for (Transform bone = head; bone != null && bone != animator.transform; bone = bone.parent)
                {
                    Quaternion local = bone.localRotation;   // a node the avatar does not list does not move
                    for (int i = 0; i < skeleton.Length; i++)
                    {
                        if (skeleton[i].name == bone.name)
                        {
                            local = skeleton[i].rotation;
                            found |= bone == head;
                            break;
                        }
                    }
                    inPose = local * inPose;
                }
                if (!found)
                {
                    return false;
                }
                square = Quaternion.Inverse(inPose);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        // A looping clip by its id, or by the id every model of that kind can fall back on; null if the bundle has neither.
        private static AnimationClip Loop(string own, string shared)
        {
            if (!ClipLibrary.Ready)
            {
                return null;
            }
            string id = own != null && ClipLibrary.Has(own) ? own : ClipLibrary.Has(shared) ? shared : null;
            return id != null && ClipLibrary.TryGet(id, out CityClip clip) && clip.Loop ? clip.Clip : null;
        }

        // Which clip the rider shows: riding (pedalling, on a bicycle), freewheeling, or stopped with a foot down.
        // Looked at each frame while ridden (three comparisons); the engine is told only when it changes, and a
        // change has to last a moment first, so that a dab of the throttle or a crawl does not flick the pose.
        private void Pose(float dt, float speed, bool pushing)
        {
            float fast = Mathf.Abs(speed);
            Riding want;
            if (_riding == Riding.Stand)
            {
                want = fast > MoveAbove || (pushing && fast > StopBelow) ? Riding.Ride : Riding.Stand;
            }
            else if (fast < StopBelow && !pushing && _standClip != null)
            {
                want = Riding.Stand;
            }
            else
            {
                want = Def.Bicycle && !pushing && _coastClip != null ? Riding.Coast : Riding.Ride;
            }
            if (want == _riding)
            {
                _otherFor = 0f;
                return;
            }
            _otherFor += dt;
            if (_otherFor < (_riding == Riding.Stand ? 0.1f : 0.3f))   // off the mark at once; everything else waits a moment
            {
                return;
            }
            AnimationClip clip = want == Riding.Stand ? _standClip : want == Riding.Coast ? _coastClip : _rideClip;
            if (clip != null && _seated.Play(clip, PoseFade))
            {
                _riding = want;
            }
            _otherFor = 0f;
        }

        /// <summary>The player is off (the game has already let them go and dropped its hand targets).</summary>
        public void Dismounted()
        {
            if (!_ridden)
            {
                return;
            }
            _ridden = false;
            enabled = false;
            if (_helmet != null)
            {
                Destroy(_helmet);
                _helmet = null;
            }
            if (_seated != null)
            {
                _seated.Stop(0.2f);
                _seated = null;
            }
            _rideClip = _coastClip = _standClip = null;
            _riding = Riding.Ride;
            _legs = 1f;
            _player = null;
            _animator = null;
            _head = _thighL = _shinL = _footL = _thighR = _shinR = _footR = null;
            if (Record != null)
            {
                Record.Position = transform.position;
                Record.Heading = transform.eulerAngles.y;
            }
        }

        private static readonly int HotFixedUpdate = HotSpots.Slot("Garage: BikeMount.FixedUpdate");
        private void FixedUpdate()
        {
            using var hotSpot = HotSpots.Time(HotFixedUpdate);
            if (FrameGuard.Off)
            {
                enabled = false;   // (its feature was switched off after its faults: Core/Guard)
                return;
            }
            try
            {
                OnFixedUpdate();
            }
            catch (System.Exception e)
            {
                Guard.Fail(FrameGuard, e, this);
            }
        }

        private void OnFixedUpdate()
        {
            if (!_ridden || _rigidbody == null)
            {
                return;
            }
            Vector3 velocity = _rigidbody.velocity;
            float speed = velocity.magnitude;
            if (_drag > 0f && speed > 1f)
            {
                _rigidbody.AddForce(velocity * (-_drag * speed));
            }
            // Pushing off: the game's scooter is a quarter of a tonne with an engine that makes little of its power at low
            // revs, so pedal power alone left a bicycle crawling away from a stop. A rider stands on the pedals: an extra
            // push along the bike while the throttle is down, strongest at a standstill and gone by PushOffUntil, scaled
            // by the same effort (energy) the pedal power is. Bicycles only; a motorbike has its engine.
            if (_drag > 0f && Def.CruiseKmh > 0f && _nwh != null && _nwh.input != null)
            {
                float throttle = _nwh.input.Throttle;
                Vector3 ahead = transform.forward;
                float along = Vector3.Dot(velocity, ahead);
                if (throttle > 0.1f && along > -0.5f && along < PushOffUntil)
                {
                    float fade = 1f - Mathf.Clamp01(along / PushOffUntil);
                    _rigidbody.AddForce(ahead * (_rigidbody.mass * PushOff * fade * throttle * Mathf.Clamp01(_effort)));
                }
            }
            // Coming off: most of the speed gone in one physics step (it hit something), or the bike on its side while moving.
            // Not when the game has just moved the bike somewhere else (a tow, a way in or out of a building).
            float was = _lastVelocity.magnitude;
            Vector3 position = _rigidbody.position;
            bool moved = (position - _lastPosition).sqrMagnitude > 2f * 2f;
            _lastPosition = position;
            if (moved)
            {
                _lastVelocity = velocity;
                return;
            }
            if (Fell <= 0f && was > 7f && (_lastVelocity - velocity).magnitude > 6f)
            {
                Fell = was;
            }
            else if (Fell <= 0f && speed > 4f && Vector3.Dot(transform.up, Vector3.up) < 0.3f)
            {
                Fell = speed;
            }
            _lastVelocity = velocity;
        }

        private static readonly int HotLateUpdate = HotSpots.Slot("Garage: BikeMount.LateUpdate");
        private void LateUpdate()
        {
            using var hotSpot = HotSpots.Time(HotLateUpdate);
            if (FrameGuard.Off)
            {
                enabled = false;   // (its feature was switched off after its faults: Core/Guard)
                return;
            }
            try
            {
                OnLateUpdate();
            }
            catch (System.Exception e)
            {
                Guard.Fail(FrameGuard, e, this);
            }
        }

        private void OnLateUpdate()
        {
            if (!_ridden || _body == null || _nwh == null)
            {
                return;
            }
            float dt = Time.deltaTime;
            float speed = _nwh.Speed;

            bool pushing = _nwh.input != null && _nwh.input.Throttle > 0.1f;
            if (_seated != null)
            {
                Pose(dt, speed, pushing);
            }
            if (_body.FrontWheel != null && _body.RearWheel != null)   // a stand-in or a model in parts; a one-mesh body has no wheels to turn
            {
                _spin = Mathf.Repeat(_spin + speed / Def.WheelRadius * dt * Mathf.Rad2Deg, 360f);
                Quaternion roll = Quaternion.AngleAxis(_spin, Vector3.right);
                _body.FrontWheel.localRotation = roll;
                _body.RearWheel.localRotation = roll;
            }
            if (_front != null)
            {
                _body.Steer.localRotation = Quaternion.AngleAxis(_front.SteerAngle, _body.SteerAxis);
            }

            Vector3 forward = transform.forward;
            if (Def.Bicycle)
            {
                bool stopped = _seated != null && _riding == Riding.Stand;
                if (stopped)
                {
                    // Standing astride with the left foot down: the clip has the right foot on its pedal, forward and
                    // level, so the cranks are brought round to there (backwards, as one does on a freewheel) and the
                    // legs are handed to the clip.
                    if (_crank != 0f)
                    {
                        _crank = Mathf.MoveTowards(_crank, _crank > Mathf.PI ? Mathf.PI * 2f : 0f, CrankHome * dt);
                        if (_crank >= Mathf.PI * 2f)
                        {
                            _crank = 0f;
                        }
                        _body.Pedals(Def, _crank);
                    }
                }
                else if (pushing && speed > 0.3f)
                {
                    _crank = Mathf.Repeat(_crank - speed * CadencePerMetre * dt, Mathf.PI * 2f);   // the top of the stroke goes forwards
                    _body.Pedals(Def, _crank);
                }
                _legs = Mathf.MoveTowards(_legs, stopped ? 0f : 1f, dt / LegsFade);
                if (_legs > 0f && _footL != null && _footR != null)
                {
                    Vector3 up = transform.up * AnkleAbovePedal - forward * 0.03f;
                    LegIk.Solve(_thighR, _shinR, _footR, transform.TransformPoint(BikeBody.PedalAt(Def, _crank, 1f)) + up, forward, _legs);
                    LegIk.Solve(_thighL, _shinL, _footL, transform.TransformPoint(BikeBody.PedalAt(Def, _crank + Mathf.PI, -1f)) + up, forward, _legs);
                }
            }
            else if (_seated == null && _footL != null && _footR != null)
            {
                Vector3 right = transform.right;
                Vector3 up = transform.up * AnkleAbovePedal;
                LegIk.Solve(_thighR, _shinR, _footR, transform.TransformPoint(new Vector3(Def.Peg.x, Def.Peg.y, Def.Peg.z)) + up, forward + right * 0.25f);
                LegIk.Solve(_thighL, _shinL, _footL, transform.TransformPoint(new Vector3(-Def.Peg.x, Def.Peg.y, Def.Peg.z)) + up, forward - right * 0.25f);
            }

            if (_helmet != null && _head != null && _player != null)
            {
                // On the head, turn and all: the helmet's pivot is the Head bone, and it turns with the bone
                // (by the bone's own axes made square to the character: see HeadSquare).
                _helmet.transform.SetPositionAndRotation(_head.position, _headKnown ? _head.rotation * _headSquare : _player.rotation);
            }
        }
    }

    /// <summary>
    /// A leg put on a point after the animation: the thigh and shin are turned so that the ankle is on
    /// the target with the knee towards a given side. Unlike CityLife's TwoBoneReach (made for arms that
    /// are already bent the right way) this decides the bend itself: a standing leg is straight.
    /// </summary>
    public static class LegIk
    {
        /// <summary>weight: 1 puts the ankle on the target; less leaves that much of the pose the animation gave the leg.</summary>
        public static void Solve(Transform thigh, Transform shin, Transform foot, Vector3 target, Vector3 kneeTowards, float weight = 1f)
        {
            if (thigh == null || shin == null || foot == null)
            {
                return;
            }
            Vector3 hip = thigh.position;
            float upper = (shin.position - hip).magnitude;
            float lower = (foot.position - shin.position).magnitude;
            if (upper < 0.01f || lower < 0.01f)
            {
                return;
            }
            Quaternion footTurn = foot.rotation;
            Quaternion thighWas = thigh.localRotation, shinWas = shin.localRotation;
            Vector3 to = target - hip;
            float reach = Mathf.Clamp(to.magnitude, Mathf.Abs(upper - lower) + 0.01f, upper + lower - 0.005f);
            Vector3 along = to.sqrMagnitude > 1e-6f ? to.normalized : Vector3.down;
            Vector3 side = kneeTowards - along * Vector3.Dot(kneeTowards, along);
            if (side.sqrMagnitude < 1e-6f)
            {
                return;
            }
            side.Normalize();
            float cos = Mathf.Clamp((upper * upper + reach * reach - lower * lower) / (2f * upper * reach), -1f, 1f);
            Vector3 knee = hip + along * (upper * cos) + side * (upper * Mathf.Sqrt(1f - cos * cos));
            thigh.rotation = Quaternion.FromToRotation(shin.position - hip, knee - hip) * thigh.rotation;
            Vector3 ankle = hip + along * reach;
            shin.rotation = Quaternion.FromToRotation(foot.position - shin.position, ankle - shin.position) * shin.rotation;
            if (weight < 1f)
            {
                thigh.localRotation = Quaternion.Slerp(thighWas, thigh.localRotation, weight);
                shin.localRotation = Quaternion.Slerp(shinWas, shin.localRotation, weight);
            }
            foot.rotation = footTurn;   // the foot stays as flat as the animation had it
        }
    }
}
