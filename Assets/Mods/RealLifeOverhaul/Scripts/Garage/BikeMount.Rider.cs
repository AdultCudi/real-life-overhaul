using System;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>The rider's part of <see cref="BikeMount"/>: the clip shown, the wheels, bars, cranks, legs and helmet each frame.</summary>
    public sealed partial class BikeMount
    {
        private static readonly int HotLateUpdate = HotSpots.Slot("Garage: BikeMount.LateUpdate");
        private void LateUpdate()
        {
            using var hotSpot = HotSpots.Time(HotLateUpdate);
            if (FrameGuard.Off)
            {
                enabled = false;   // switched off after its faults
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
            if (_body.FrontWheel != null && _body.RearWheel != null)   // a one-mesh body has no wheels to turn
            {
                _spin = Mathf.Repeat(_spin + speed / Def.WheelRadius * dt * Mathf.Rad2Deg, 360f);
                Quaternion roll = Quaternion.AngleAxis(_spin, Vector3.right);
                _body.FrontWheel.localRotation = roll;
                _body.RearWheel.localRotation = roll;
            }
            if (_front != null)
            {
                // The physics steers once a step; the bars follow it eased, not in steps.
                _steerShown = BikeRules.Ease(_steerShown, _front.SteerAngle, dt, SteerEase);
                _body.Steer.localRotation = Quaternion.AngleAxis(_steerShown, _body.SteerAxis);
            }

            Vector3 forward = transform.forward;
            if (Def.Bicycle)
            {
                bool stopped = _seated != null && _riding == BikeRules.Pose.Stand;
                if (stopped)
                {
                    // Astride with the left foot down: the clip has the right pedal forward and level, so the cranks are
                    // brought round to there, backwards as on a freewheel, and the legs handed to the clip.
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
                    float weight = BikeRules.Smooth(_legs);   // the feet leave and reach the pedals without a jolt
                    Vector3 up = transform.up * AnkleAbovePedal - forward * 0.03f;
                    LegIk.Solve(_thighR, _shinR, _footR, transform.TransformPoint(BikeBody.PedalAt(Def, _crank, 1f)) + up, forward, weight);
                    LegIk.Solve(_thighL, _shinL, _footL, transform.TransformPoint(BikeBody.PedalAt(Def, _crank + Mathf.PI, -1f)) + up, forward, weight);
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
                // The helmet's pivot is the Head bone; it turns with the bone, squared to the character (HeadSquare).
                _helmet.transform.SetPositionAndRotation(_head.position, _headKnown ? _head.rotation * _headSquare : _player.rotation);
            }
        }

        // The Head bone's axes are the rig's, not the character's. In the avatar's T-pose the head is straight, so the
        // bone's rotation there is what stands between them. False when the avatar does not say: the helmet then keeps
        // the player's heading.
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

        // A looping clip by its own id, else the shared one of its kind; null if the bundle has neither.
        private static AnimationClip Loop(string own, string shared)
        {
            if (!ClipLibrary.Ready)
            {
                return null;
            }
            string id = own != null && ClipLibrary.Has(own) ? own : ClipLibrary.Has(shared) ? shared : null;
            return id != null && ClipLibrary.TryGet(id, out CityClip clip) && clip.Loop ? clip.Clip : null;
        }

        // Which clip the rider shows (BikeRules.Want). A change has to last its moment first, and none is made while the
        // last is fading in: the animator would replace the clip fading out, which shows as a jump.
        private void Pose(float dt, float speed, bool pushing)
        {
            _poseAge += dt;
            BikeRules.Pose want = BikeRules.Want(_riding, speed, pushing, _standClip != null, _coastClip != null, Def.Bicycle);
            if (want == _riding)
            {
                _otherFor = 0f;
                return;
            }
            _otherFor += dt;
            if (_otherFor < BikeRules.HoldFor(_riding, want) || _poseAge < BikeRules.PoseFade)
            {
                return;
            }
            AnimationClip clip = want == BikeRules.Pose.Stand ? _standClip : want == BikeRules.Pose.Coast ? _coastClip : _rideClip;
            if (clip != null && _seated.Play(clip, BikeRules.PoseFade))
            {
                _riding = want;
                _poseAge = 0f;
            }
            _otherFor = 0f;
        }
    }
}
