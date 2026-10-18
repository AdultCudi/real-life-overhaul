using System;
using System.Collections.Generic;
using System.Globalization;
using BAModAPI;
using GleyTrafficSystem;
using Helpers;
using UI.Notification;
using UnityEngine;
using Random = UnityEngine.Random;

namespace RealLifeOverhaul
{
    /// <summary>The tow truck: the hook, the winch, and the wreck drawn up the bed and carried on it.</summary>
    public sealed partial class TowService : MonoBehaviour
    {
        private const float HookOut = 0.70f;          // metres beyond the wreck's near end the operator kneels at the hook
        private const float CableAbove = 0.45f;       // metres over the road the cable is hooked on to a car

        // The wreck's way to the bed, worked out when the winch starts: in the truck's own space.
        private Quaternion _carFrom;        // its rotation as it stood (world)
        private Quaternion _carUp;          // ...set on its wheels with the heading it had (world)
        private Vector3 _localCentre;       // the middle of its body from its own origin, in its own space
        private float _x0, _z0, _yaw0;      // where it starts on the road (across, ahead) and its heading, degrees from the truck's
        private float _load;                // its heading on the bed: 0 nose to the cab, 180 tail to it
        private float _ground;              // the road under it, in the truck's own space
        private Vector3 _onBed;             // its origin and rotation on the bed, once it is up it (the bed's own space)
        private Quaternion _onBedTurn;

        // The operator walks to the wreck's near end and kneels there, hooking the cable on; the cable runs out with them.
        private void BeginHook(float now)
        {
            if (_job == null || _job.Car == null)
            {
                Leave(now);
                return;
            }
            Transform truck = _truck.transform;
            TrafficTools.Footprint(_job.Car, out Vector3 centre, out _, out float halfLength, out _);
            Vector3 towards = truck.forward;
            Vector3 hook = centre + towards * (halfLength + HookOut);
            hook.y = truck.position.y;
            _hookedFrom = 0f;
            _operator.Go(hook, Pace.Walk, centre, WalkAllow);
            _operator.Kneel();
            Go(Step.Hooking);
        }

        // The winch starts: the wreck's way to the bed worked out once, in the truck's own space.
        private void BeginWinch(float now)
        {
            if (_job == null || _job.Car == null)
            {
                Leave(now);
                return;
            }
            Transform truck = _truck.transform;
            Transform car = _job.Car.transform;
            TrafficTools.Footprint(_job.Car, out Vector3 centre, out Vector3 forward, out float halfLength, out float halfWidth);
            _job.Centre = centre;
            _job.HalfLength = halfLength;
            _job.HalfWidth = halfWidth;
            _localCentre = car.InverseTransformPoint(centre);
            Vector3 local = truck.InverseTransformPoint(centre);
            Vector3 heading = truck.InverseTransformDirection(forward);
            _x0 = local.x;
            _z0 = local.z;
            _yaw0 = Mathf.Atan2(heading.x, heading.z) * Mathf.Rad2Deg;
            _load = TowRules.LoadHeading(_yaw0);
            _ground = CitySpots.Ground(centre, out float y) ? truck.InverseTransformPoint(new Vector3(centre.x, y, centre.z)).y : 0f;
            _carFrom = car.rotation;
            _carUp = truck.rotation * Quaternion.Euler(0f, _yaw0, 0f);
            _operator.Go(PanelSpot(), Pace.Walk, truck.TransformPoint(new Vector3(_side * TowTruck.PanelOut, 1f, TowTruck.PanelAhead)), WalkAllow);
            _operator.Work(Clip("pump_panel"));
            bool upright = Vector3.Dot(car.up, Vector3.up) > 0.85f;
            Logger?.Info("Tow check (winch): the wreck stands " + (_z0 - TowRules.FootAhead(halfLength)).ToString("F1", CultureInfo.InvariantCulture)
                + " m from the foot of the bed along the truck (wanted 0 or less: drawn forward), "
                         + _x0.ToString("F1", CultureInfo.InvariantCulture) + " m across, " + _yaw0.ToString("F0") + " degrees round, " + (upright ? "on its wheels" : "on its side or roof") + ": "
                         + (_z0 <= TowRules.FootAhead(halfLength) + 0.5f ? "PASS" : "FAIL"));
            if (upright)
            {
                BeginDrag();
            }
            else
            {
                _rightFrom = car.rotation;
                _rightMiddle = centre;
                _rightTo = truck.TransformPoint(new Vector3(_x0, _ground, _z0)) + Vector3.up * HeightOfMiddle();
                Go(Step.Righting, TowRules.RightSeconds);
            }
        }

        // The pull along the road, from the pose it has now: what the curve's start leaves over (a car standing a little
        // askew of the road under it) is eased out over the pull, so that it starts exactly where it stands.
        private void BeginDrag()
        {
            Transform car = _job.Car.transform;
            _carFrom = car.rotation;
            _dragOff = car.TransformPoint(_localCentre) - (_truck.transform.TransformPoint(new Vector3(_x0, _ground, _z0)) + Vector3.up * HeightOfMiddle());
            float foot = TowRules.FootAhead(_job.HalfLength);
            float reach = Mathf.Sqrt(_x0 * _x0 + (foot - _z0) * (foot - _z0));
            Go(Step.Dragging, TowRules.PullSeconds(reach, TowRules.DragPace));
        }

        private Quaternion _rightFrom;      // a car on its side or roof: its rotation as it lay...
        private Vector3 _rightMiddle;       // ...its middle...
        private Vector3 _rightTo;           // ...and its middle once it is back on its wheels
        private Vector3 _dragOff;           // what the pull eases out over its course

        private void EndPull(float now)
        {
            switch (_step)
            {
                case Step.Righting:
                    BeginDrag();
                    break;
                case Step.Dragging:
                    float climb = TowRules.RestAlong(_job.HalfLength) - TowRules.FootAlong(_job.HalfLength);
                    Go(Step.Climbing, TowRules.PullSeconds(climb, TowRules.ClimbPace));
                    break;
                case Step.Climbing:
                    // On the bed: from now on it is carried as it stands on it.
                    PoseOnBed(1f);
                    Transform bed = _truck.Bed;
                    Transform car = _job.Car.transform;
                    _onBed = bed.InverseTransformPoint(car.position);
                    _onBedTurn = Quaternion.Inverse(bed.rotation) * car.rotation;
                    _carried = true;
                    _truck.Cable(false, Vector3.zero);
                    Go(Step.Raising, TowRules.UpSeconds);
                    break;
                case Step.Raising:
                    _truck.Deck(0f);
                    Carry();
                    _paperFrom = 0f;
                    Transform truck = _truck.transform;
                    Vector3 door = Door();
                    _operator.Go(door, Pace.Walk, truck.TransformPoint(new Vector3(0f, 1.2f, TowTruck.DoorAhead)), WalkAllow);
                    _operator.Work(SceneClips.Clipboard);
                    Go(Step.Paperwork);
                    break;
            }
        }

        // Every frame while it works: the bed, the cable and the wreck where the step and its share of time put them.
        private void Animate(float now)
        {
            if (_truck == null || !_truck.Out)
            {
                return;
            }
            float u = _stepFor > 0f ? Mathf.Clamp01((now - _stepAt) / _stepFor) : 1f;
            switch (_step)
            {
                case Step.Lowering:
                    _truck.Deck(u);
                    break;
                case Step.Hooking:
                    if (_operator.Present)
                    {
                        _truck.Cable(true, _operator.Position + Vector3.up * 0.6f);
                    }
                    break;
                case Step.Righting:
                    if (_job != null && _job.Car != null)
                    {
                        // Turned back on to its wheels about its middle, which lifts a little as it goes over and comes to
                        // rest as high over the road as it stands on its wheels.
                        float s = TowRules.Smooth(u);
                        Quaternion turn = Quaternion.Slerp(_rightFrom, _carUp, s);
                        Vector3 middle = Vector3.Lerp(_rightMiddle, _rightTo, s) + Vector3.up * (Mathf.Sin(s * Mathf.PI) * 0.35f);
                        _job.Car.transform.SetPositionAndRotation(middle - turn * _localCentre, turn);
                        _truck.Cable(true, Hook());
                    }
                    break;
                case Step.Dragging:
                    if (_job != null && _job.Car != null)
                    {
                        float foot = TowRules.FootAhead(_job.HalfLength);
                        TowRules.Drag(TowRules.Smooth(u), _x0, _z0, _yaw0, 0f, foot, _load, out float x, out float z, out float yaw);
                        PoseOnRoad(x, z, yaw, u);
                        _truck.Cable(true, Hook());
                    }
                    break;
                case Step.Climbing:
                    if (_job != null && _job.Car != null)
                    {
                        PoseOnBed(TowRules.Smooth(u));
                        _truck.Cable(true, Hook());
                    }
                    break;
                case Step.Raising:
                    _truck.Deck(1f - u);
                    break;
            }
        }

        // The wreck on the road behind the bed: its middle at (across, ahead) in the truck's space on the road, turned so; its
        // tilt as it stood easing out to level over the pull.
        private void PoseOnRoad(float x, float z, float yaw, float u)
        {
            Transform truck = _truck.transform;
            Quaternion level = truck.rotation * Quaternion.Euler(0f, yaw, 0f);
            Quaternion stood = _carFrom * Quaternion.Inverse(_carUp);   // what is left of its tilt as it stood (none on a level road)
            Quaternion turn = Quaternion.Slerp(stood, Quaternion.identity, TowRules.Smooth(u)) * level;
            float height = Mathf.Lerp(_ground, 0f, TowRules.Smooth(u));
            Vector3 middle = truck.TransformPoint(new Vector3(x, height, z)) + Vector3.up * HeightOfMiddle() + _dragOff * (1f - TowRules.Smooth(u));
            _job.Car.transform.SetPositionAndRotation(middle - turn * _localCentre, turn);
        }

        // The wreck going up the bed: share s of the way from the foot to its place on it.
        private void PoseOnBed(float s)
        {
            float along = Mathf.Lerp(TowRules.FootAlong(_job.HalfLength), TowRules.RestAlong(_job.HalfLength), s);
            TowRules.CarOnBed(along, _job.HalfLength, out float up, out float ahead, out float pitch);
            Transform truck = _truck.transform;
            Quaternion turn = truck.rotation * Quaternion.Euler(-pitch, 0f, 0f) * Quaternion.Euler(0f, _load, 0f);
            Vector3 point = truck.TransformPoint(new Vector3(0f, up, ahead));
            Vector3 middle = point + turn * new Vector3(0f, HeightOfMiddle(), 0f);
            _job.Car.transform.SetPositionAndRotation(middle - turn * _localCentre, turn);
        }

        // The middle of the car's body over the line its wheels run on: its own origin is on the ground under it, so the
        // middle stands as high over the origin as it does in the car's own space.
        private float HeightOfMiddle()
        {
            return _localCentre.y;
        }

        // The end of the wreck the cable is hooked to: the one nearer the truck, a little over the road.
        private Vector3 Hook()
        {
            Transform car = _job.Car.transform;
            Vector3 centre = car.TransformPoint(_localCentre);
            Vector3 towards = _truck.transform.position - centre;
            Vector3 end = car.forward * (Vector3.Dot(car.forward, towards) >= 0f ? 1f : -1f);
            return centre + end * _job.HalfLength - car.up * (_localCentre.y - CableAbove);
        }

        private static readonly int HotLateUpdate = HotSpots.Slot("Traffic: TowService.LateUpdate");

        // While it is carried, every frame after the truck has moved: the wreck where it stands on the bed.
        private void LateUpdate()
        {
            using var hotSpot = HotSpots.Time(HotLateUpdate);
            if (FrameGuard.Off)
            {
                enabled = false;
                return;
            }
            try
            {
                OnLateUpdate();
            }
            catch (Exception e)
            {
                Guard.Fail(FrameGuard, e, this);
            }
        }

        private void OnLateUpdate()
        {
            if (_carried)
            {
                Carry();
            }
        }

        private void Carry()
        {
            if (_job == null || _job.Car == null || _truck == null)
            {
                return;
            }
            Transform bed = _truck.Bed;
            _job.Car.transform.SetPositionAndRotation(bed.TransformPoint(_onBed), bed.rotation * _onBedTurn);
        }
    }
}
