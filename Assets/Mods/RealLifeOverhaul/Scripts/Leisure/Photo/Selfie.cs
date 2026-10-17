using System;
using BAModAPI;
using UnityEngine;

namespace RealLifeOverhaul.Leisure
{
    /// <summary>
    /// Photo mode's selfie: the camera is the phone, held at arm's length and moved round the face. The game has no
    /// selfie pose, so after the animation the arm reaches for the lens and the head turns into it. Off unless a selfie
    /// is on; then per frame two clip steps and five bone turns, nothing allocated.
    /// </summary>
    public sealed class Selfie : MonoBehaviour
    {
        private static readonly FeatureGuard FrameGuard = Guard.For("leisure", "Selfie", typeof(Selfie));

        private const float Near = 0.40f;          // metres from the eyes at nearest: the elbow well bent
        private const float Far = 0.85f;           // metres at farthest: a straight arm
        private const float StartDistance = 0.62f; // metres
        private const float StartAbove = 12f;      // degrees above eye level
        private const float YawMost = 40f;         // degrees either way round the face
        private const float PitchLeast = -12f;     // degrees
        private const float PitchMost = 42f;       // degrees
        private const float NearPlane = 0.15f;     // metres: the hand at the lens is nearer and is not drawn
        private const float EyeHeight = 0.08f;     // metres above the head joint
        private const float EyeForward = 0.05f;    // metres in front of it
        private const float HandBelow = 0.07f;     // metres below the lens
        private const float HandAside = 0.05f;     // metres to the phone arm's side
        private const float HandShort = 0.10f;     // metres short of the lens, towards the face
        private const float NeckShare = 0.4f;      // of the turn to the lens; the head takes the rest
        private const float ReplayGap = 0.6f;      // seconds a wave rests before it starts again
        private const float HeldSpeed = 0.01f;     // a clip's least speed: still, to the eye
        private const float StepMost = 0.05f;      // seconds a clip is stepped at most in one frame

        private const int PoseCount = 4;
        private static readonly string[] PoseClips = { null, "wave_hello", "cheer_once", "talk_a" };
        private static readonly string[] PoseNames =
        {
            "reallife_photo_pose_phone", "reallife_photo_pose_wave", "reallife_photo_pose_fist", "reallife_photo_pose_talk"
        };
        private const string HoldMark = "top";     // cheer_once's mark where the fist is highest

        public static IModLogger Logger;

        /// <summary>True from a successful <see cref="Begin"/> to <see cref="End"/>.</summary>
        public bool On { get; private set; }

        /// <summary>Metres from the phone to the eyes.</summary>
        public float Distance => _distance;

        /// <summary>The locale key naming the pose that is on.</summary>
        public string PoseKey => PoseNames[_pose];

        private Camera _camera;
        private float _nearBefore;
        private ThirdPersonCharacter _body;
        private Animator _animator;
        private Transform _root, _head, _neck;
        private Transform _upperR, _lowerR, _handR, _upperL, _lowerL, _handL;
        private Quaternion _rootBefore;
        private bool _turned;
        private Vector3 _facing;                   // level
        private Vector3 _headAxis, _neckAxis;      // the face's direction in each bone's own frame
        private float _yaw, _pitch, _distance;
        private Vector3 _lens;

        private int _pose;
        private ActivityAnimator _clips;
        private bool _clipsOffered;
        private CityClip _clip;
        private float _clipTime;
        private float _holdAt;
        private bool _held;

        private void Awake()
        {
            enabled = false;
        }

        private void OnDestroy()
        {
            End();
        }

        /// <summary>The lens starts on the side of <paramref name="from"/>; false if the body cannot pose.</summary>
        public bool Begin(Camera camera, PlayerController player, Vector3 from)
        {
            End();
            ThirdPersonCharacter body = player != null ? player.Character : null;
            Animator animator = body != null ? body.animator : null;
            if (camera == null || animator == null || !animator.isHuman)
            {
                return false;
            }
            _head = animator.GetBoneTransform(HumanBodyBones.Head);
            _neck = animator.GetBoneTransform(HumanBodyBones.Neck) ?? _head;
            _upperR = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
            _lowerR = animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
            _handR = animator.GetBoneTransform(HumanBodyBones.RightHand);
            _upperL = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            _lowerL = animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
            _handL = animator.GetBoneTransform(HumanBodyBones.LeftHand);
            if (_head == null || _upperR == null || _lowerR == null || _handR == null || _upperL == null || _lowerL == null || _handL == null)
            {
                Logger?.Warn("Photo mode: the player's skeleton has no head or arm joints to pose; no selfie.");
                return false;
            }
            _camera = camera;
            _body = body;
            _animator = animator;
            _root = body.transform;

            // Standing, they turn to the free camera, so its view is behind them; seated, they stay as they sit.
            _rootBefore = _root.rotation;
            Vector3 towards = from - _root.position;
            towards.y = 0f;
            if (body.isSittingOn == null && towards.sqrMagnitude > 0.04f)
            {
                _facing = towards.normalized;
                _root.rotation = Quaternion.LookRotation(_facing, Vector3.up);
                _turned = true;
            }
            else
            {
                _facing = Vector3.ProjectOnPlane(_root.forward, Vector3.up).normalized;
                if (_facing.sqrMagnitude < 0.5f)
                {
                    _facing = Vector3.forward;
                }
            }
            // Kept in each bone's own frame, so the head turns the same way every frame and nothing builds up.
            _headAxis = _head.InverseTransformDirection(_facing);
            _neckAxis = _neck.InverseTransformDirection(_facing);

            _nearBefore = camera.nearClipPlane;
            camera.nearClipPlane = NearPlane;

            _yaw = 0f;
            _pitch = StartAbove;
            _distance = StartDistance;
            _clips = ActivityAnimators.For(animator);
            // Not over a clip of the mod's already on the player (an umbrella, say): only the plain pose then.
            _clipsOffered = _clips != null && ActivityAnimators.Working && _clips.Usable && !_clips.Active && ClipLibrary.Ready;
            _pose = 0;
            _clip = null;
            On = true;
            enabled = true;
            Place();
            if (Meta.Dev.Verbose)
            {
                Logger?.Info("Photo mode: selfie check (lens " + _distance.ToString("0.00") + " m from the eyes, wanted " + Near.ToString("0.00") + " to "
                             + Far.ToString("0.00") + "; body " + (_turned ? "turned to the free camera" : "left as it sat") + "; clip poses "
                             + (_clipsOffered ? "offered" : "not offered (bundle not ready, or a clip of the mod's already on the player)") + "): PASS");
            }
            return true;
        }

        /// <summary>One frame of steering: the mouse round the face, the wheel nearer or farther.</summary>
        public void Steer(float mouseX, float mouseY, float wheel, float roll)
        {
            if (!On)
            {
                return;
            }
            _yaw = Mathf.Clamp(_yaw + mouseX, -YawMost, YawMost);
            _pitch = Mathf.Clamp(_pitch + mouseY, PitchLeast, PitchMost);
            if (wheel != 0f)
            {
                _distance = Mathf.Clamp(_distance - wheel * 0.03f, Near, Far);
            }
            Place(roll);
        }

        public void Recentre()
        {
            _yaw = 0f;
            _pitch = StartAbove;
            _distance = StartDistance;
        }

        /// <summary>Moves to the next pose there is and returns the locale key of its name.</summary>
        public string NextPose()
        {
            if (!On)
            {
                return PoseKey;
            }
            int next = _pose;
            for (int i = 0; i < PoseCount; i++)
            {
                next = (next + 1) % PoseCount;
                if (next == 0 || (_clipsOffered && ClipLibrary.Has(PoseClips[next])))
                {
                    break;
                }
            }
            if (next != _pose)
            {
                _pose = next;
                StartPose();
            }
            return PoseKey;
        }

        /// <summary>Puts back everything Begin changed. Safe at any time; never throws.</summary>
        public void End()
        {
            if (!On)
            {
                return;
            }
            On = false;
            enabled = false;
            try
            {
                StopClip();
                if (_turned && _root != null)
                {
                    _root.rotation = _rootBefore;
                }
                if (_camera != null)
                {
                    _camera.nearClipPlane = _nearBefore;
                }
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
            _turned = false;
            _camera = null;
            _body = null;
            _animator = null;
            _root = _head = _neck = null;
            _upperR = _lowerR = _handR = _upperL = _lowerL = _handL = null;
            _clips = null;
            _clip = null;
        }

        private Vector3 Eyes()
        {
            return _head.position + Vector3.up * EyeHeight + _facing * EyeForward;
        }

        private void Place(float roll = 0f)
        {
            Vector3 eyes = Eyes();
            Vector3 right = Vector3.Cross(Vector3.up, _facing);
            Vector3 along = Quaternion.AngleAxis(_yaw, Vector3.up) * (Quaternion.AngleAxis(-_pitch, right) * _facing);
            _lens = eyes + along * _distance;
            _camera.transform.SetPositionAndRotation(_lens, Quaternion.LookRotation(-along, Vector3.up) * Quaternion.Euler(0f, 0f, roll));
        }

        private void StartPose()
        {
            StopClip();
            string name = PoseClips[_pose];
            if (name == null || !ClipLibrary.TryGet(name, out CityClip clip))
            {
                return;
            }
            if (_clips.Play(clip.Clip, 0.2f, 1f, ClipEnd.Hold))
            {
                _clip = clip;
                _clipTime = 0f;
                _held = false;
                _holdAt = name == "cheer_once" ? clip.Mark(HoldMark, 0.5f) : -1f;
                ClipLibrary.Played(name, "the player (selfie)");
            }
        }

        private void StopClip()
        {
            if (_clip != null && _clips != null)
            {
                _clips.Dispose();   // at once: the game is paused, a fade would never run
            }
            _clip = null;
        }

        private static readonly int HotUpdate = HotSpots.Slot("Leisure: Selfie.Update");
        // The clip pose stepped by the real clock, before the animation pass, which advances nothing while paused.
        private void Update()
        {
            using var hotSpot = HotSpots.Time(HotUpdate);
            if (FrameGuard.Off)
            {
                enabled = false;
                return;
            }
            try
            {
                OnUpdate();
            }
            catch (Exception e)
            {
                Guard.Fail(FrameGuard, e, this);
            }
        }

        private void OnUpdate()
        {
            try
            {
                if (_body == null || _animator == null || !_body.gameObject.activeInHierarchy)
                {
                    End();   // the player went away (a scene change)
                    return;
                }
                if (_clip == null || _clips == null)
                {
                    return;
                }
                if (!_clips.Active)
                {
                    _clip = null;   // handed back elsewhere: the clip service switched off
                    return;
                }
                float delta = Mathf.Min(Time.unscaledDeltaTime, StepMost);
                _clipTime += delta;
                if (_holdAt >= 0f)
                {
                    if (!_held && _clipTime >= _holdAt)
                    {
                        _clips.SetSpeed(HeldSpeed);
                        _held = true;
                    }
                }
                else if (!_clip.Loop && _clipTime >= _clip.Length + ReplayGap)
                {
                    _clips.Play(_clip.Clip, 0.25f, 1f, ClipEnd.Hold);
                    _clipTime = 0f;
                }
                if (Time.timeScale <= 0f)
                {
                    // In a Going Public session the clock runs and the clip service steps it already.
                    _clips.Advance(delta);
                    _clips.Evaluate(delta);
                }
            }
            catch (Exception e)
            {
                Logger?.Error(e);
                Logger?.Warn("Photo mode: the selfie was ended after the error above; the body and the camera were put back.");
                End();
            }
        }

        private static readonly int HotLateUpdate = HotSpots.Slot("Leisure: Selfie.LateUpdate");
        // After the animation, from directions worked out afresh, whether or not the animator wrote the bones.
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
            try
            {
                if (_head == null)
                {
                    End();
                    return;
                }
                Vector3 eyes = Eyes();
                Vector3 toLens = _lens - eyes;
                float apart = toLens.magnitude;
                if (apart < 0.05f)
                {
                    return;
                }
                toLens /= apart;
                bool rightHand = _pose == 0;   // the clip poses use the right hand, so the phone goes left
                float side = rightHand ? 1f : -1f;
                Vector3 bodyRight = Vector3.Cross(Vector3.up, toLens).normalized;
                Vector3 hand = _lens - Vector3.up * HandBelow + bodyRight * (side * HandAside) - toLens * HandShort;
                Vector3 elbowOut = bodyRight * (side * 0.6f) - Vector3.up;
                if (rightHand)
                {
                    BonePose.Reach(_upperR, _lowerR, _handR, hand, elbowOut);
                }
                else
                {
                    BonePose.Reach(_upperL, _lowerL, _handL, hand, elbowOut);
                }

                if (_neck != _head)
                {
                    Vector3 neckNow = _neck.TransformDirection(_neckAxis);
                    _neck.rotation = Quaternion.FromToRotation(neckNow, Vector3.Slerp(_root.forward, toLens, NeckShare)) * _neck.rotation;
                }
                Vector3 headNow = _head.TransformDirection(_headAxis);
                _head.rotation = Quaternion.FromToRotation(headNow, toLens) * _head.rotation;
            }
            catch (Exception e)
            {
                Logger?.Error(e);
                Logger?.Warn("Photo mode: the selfie was ended after the error above; the body and the camera were put back.");
                End();
            }
        }
    }
}
