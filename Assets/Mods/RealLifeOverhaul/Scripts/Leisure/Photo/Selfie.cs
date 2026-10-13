using System;
using BAModAPI;
using UnityEngine;

namespace RealLifeOverhaul.Leisure
{
    /// <summary>
    /// Photo mode's selfie: the camera is the phone. It is held at arm's length in front of the
    /// player's face, a little above the eyes, looking back at them; the player turns to face it
    /// (their back to whatever the free camera was looking at, so that is the background) and holds
    /// the phone out towards the lens. The mouse moves the phone round the face within a small arc,
    /// the wheel brings it nearer or takes it farther, within what an arm reaches.
    ///
    /// The game has no selfie, phone-held-out or hand-sign animation: its only phone poses are
    /// TalkingPhone (at the ear) and TextingPhone (at the chest, head down at the screen), neither
    /// of which can face a lens in front of the face, and its faces have no expressions (the head's
    /// blendshapes are the character creator's shapes, and its "expressions" are emoji bubbles on
    /// the interface, which a picture never has). So the phone arm is set by hand over whatever the
    /// body is doing, the way People/KneelPose and Crime/CuffPose set theirs (People/BonePose): the
    /// upper arm and forearm reach for a point just in front of the lens and a little below it, and
    /// the neck and head are turned to look into it. The hand itself is nearer the lens than the
    /// camera draws (the near plane is put at <see cref="NearPlane"/> while the selfie is on), so
    /// the arm leaves the picture at its edge as it does in a real selfie, and no phone is drawn.
    /// The poses besides the plain one are the mod's own clips (CityAssets): a wave
    /// (<c>wave_hello</c>), a fist up (<c>cheer_once</c>, held at its top) and talking to the
    /// camera (<c>talk_a</c>); with one of those the phone goes to the left hand, since all three
    /// are made with the right. Photo mode keeps the game paused, so the clip player is stepped
    /// here by the real clock (ActivityAnimator.Advance and Evaluate) rather than by the game's;
    /// in a Going Public session, where the clock goes on running, the game's own step does it.
    /// The clip poses are offered only when the bundle is loaded and nothing of the mod's was
    /// already playing on the player (an umbrella, say): that is left alone, and only the plain
    /// pose is offered.
    ///
    /// Everything changed is put back in <see cref="End"/>: the body's facing, the camera's near
    /// plane, the clip player (handed back at once). Nothing is left on the character.
    ///
    /// Cost: this component is switched off unless a selfie is on, so photo mode off (or its free
    /// camera) costs nothing here. While it is on: per frame two clip steps when a clip pose is on,
    /// and in LateUpdate about a dozen vector operations and five bone turns. Nothing is allocated.
    /// </summary>
    public sealed class Selfie : MonoBehaviour
    {
        private static readonly FeatureGuard FrameGuard = Guard.For("leisure", "Selfie", typeof(Selfie));   // its Update, LateUpdate and FixedUpdate (Core/Guard)

        private const float Near = 0.40f;          // metres from the eyes the phone comes at nearest: the elbow well bent...
        private const float Far = 0.85f;           // ...and at farthest: a straight arm with the phone at the fingertips
        private const float StartDistance = 0.62f; // metres where it starts: the arm out, the elbow soft
        private const float StartAbove = 12f;      // degrees above eye level it starts at, looking down a little
        private const float YawMost = 40f;         // degrees round the face either way the mouse can take it
        private const float PitchLeast = -12f;     // degrees: from a little below eye level...
        private const float PitchMost = 42f;       // ...to well above, the classic angle
        private const float NearPlane = 0.15f;     // metres: the hand at the lens is nearer than this and is not drawn
        private const float EyeHeight = 0.08f;     // metres from the head joint (the top of the neck) up to the eyes...
        private const float EyeForward = 0.05f;    // ...and forward of it
        private const float HandBelow = 0.07f;     // metres the hand is below the lens...
        private const float HandAside = 0.05f;     // ...to the side of the phone arm...
        private const float HandShort = 0.10f;     // ...and short of it towards the face: the phone between them
        private const float NeckShare = 0.4f;      // how much of the turn to the lens the neck takes; the head the rest
        private const float ReplayGap = 0.6f;      // seconds a wave rests at its end before it starts again
        private const float HeldSpeed = 0.01f;     // the speed a clip is held at (its least): still, to the eye
        private const float StepMost = 0.05f;      // seconds a clip is stepped by at most in one frame

        private const int PoseCount = 4;
        private static readonly string[] PoseClips = { null, "wave_hello", "cheer_once", "talk_a" };
        private static readonly string[] PoseNames =
        {
            "reallife_photo_pose_phone", "reallife_photo_pose_wave", "reallife_photo_pose_fist", "reallife_photo_pose_talk"
        };
        private const string HoldMark = "top";     // the moment of cheer_once that the fist is highest (CityAssets)

        public static IModLogger Logger;

        /// <summary>True from a successful Begin to End.</summary>
        public bool On { get; private set; }

        /// <summary>How far the phone is from the eyes, in metres: photo mode's focus distance while the selfie is on.</summary>
        public float Distance => _distance;

        /// <summary>The words for the pose that is on (a key of the locale file).</summary>
        public string PoseKey => PoseNames[_pose];

        private Camera _camera;
        private float _nearBefore;
        private ThirdPersonCharacter _body;
        private Animator _animator;
        private Transform _root, _head, _neck;
        private Transform _upperR, _lowerR, _handR, _upperL, _lowerL, _handL;
        private Quaternion _rootBefore;
        private bool _turned;
        private Vector3 _facing;                   // level, the way the body faces: towards where the lens starts
        private Vector3 _headAxis, _neckAxis;      // the way the face points, in each bone's own frame
        private float _yaw, _pitch, _distance;
        private Vector3 _lens;                     // where the camera is, this frame

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

        /// <summary>
        /// Starts a selfie for the player as they stand, with the lens in front of them on the side
        /// <paramref name="from"/> is (the free camera). False, with nothing changed, if the player's
        /// skeleton is not one this can pose. The caller has already ruled out a vehicle.
        /// </summary>
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

            // Seated (a bench, a chair), the body stays as the seat has it and the lens comes in front of it.
            // Standing, they turn to face the free camera: what it was looking at is behind them.
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
            // The face points along the body now: that direction, kept in each bone's own frame, is how the head
            // is turned later from wherever the animation has it, the same every frame (nothing builds up).
            _headAxis = _head.InverseTransformDirection(_facing);
            _neckAxis = _neck.InverseTransformDirection(_facing);

            _nearBefore = camera.nearClipPlane;
            camera.nearClipPlane = NearPlane;

            _yaw = 0f;
            _pitch = StartAbove;
            _distance = StartDistance;
            _clips = ActivityAnimators.For(animator);
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

        /// <summary>
        /// One frame of steering, from photo mode's Run: the mouse round the face, the wheel nearer or farther,
        /// <paramref name="roll"/> in degrees as the free camera has it. Puts the camera.
        /// </summary>
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

        /// <summary>Back to where the lens started: in front, a little above the eyes.</summary>
        public void Recentre()
        {
            _yaw = 0f;
            _pitch = StartAbove;
            _distance = StartDistance;
        }

        /// <summary>
        /// The next pose there is (the plain one always; the clip poses when they were offered and the bundle
        /// has them). Returns the key of its name.
        /// </summary>
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

        /// <summary>Puts back everything Begin changed. Safe to call at any time, and never throws.</summary>
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

        // ---------------------------------------------------------------- the camera

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

        // ---------------------------------------------------------------- the body

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

        // The clip pose, stepped by the real clock: the game's is stopped. Before the animation pass, so the engine's
        // own evaluation this frame (which advances nothing while paused) draws where this one left it.
        private void Update()
        {
            if (FrameGuard.Off)
            {
                enabled = false;   // (its feature was switched off after its faults: Core/Guard)
                return;
            }
            try
            {
                OnUpdate();
            }
            catch (System.Exception e)
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
                    End();   // the player went away under us (a scene change); photo mode sees On go false
                    return;
                }
                if (_clip == null || _clips == null)
                {
                    return;
                }
                if (!_clips.Active)
                {
                    _clip = null;   // handed back by somebody else (the clip service switched off)
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
                    _clips.Play(_clip.Clip, 0.25f, 1f, ClipEnd.Hold);   // the wave again, crossed from where it ended
                    _clipTime = 0f;
                }
                if (Time.timeScale <= 0f)
                {
                    // Paused, nothing else steps the clip player. In a Going Public session photo mode leaves the clock running
                    // (Core/GoingPublic), and the engine and the clip service step it as on any other frame: a step here would be a second.
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

        // After the animation: the phone arm out to the lens, the head turned into it. Every turn is to a direction
        // worked out afresh, so it is the same whether or not the animator wrote the bones again this frame.
        private void LateUpdate()
        {
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
                bool rightHand = _pose == 0;
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
