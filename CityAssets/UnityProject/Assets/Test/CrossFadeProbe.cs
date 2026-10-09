using System;
using System.Collections.Generic;
using System.IO;
using RealLifeOverhaul;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// The proof that the animation path works, run by the engine's own frame loop in play mode
/// (AnimBundleBuilder.Prove starts it). Two stand-ins with the game's skeleton are driven by the same
/// Animator Controller; one is left alone (the twin), the other goes through the mod's own player:
///
///   1. a clip from the shipped bundle is cross-faded in over the controller, a second one with a tool in
///      both hands follows, and the character is handed back: the pose must pass smoothly from the
///      controller's to the clip's and back, the controller is never touched, the root never moves, the
///      second hand stays on the tool;
///   2. a clip is played on the MASKED layer (left arm only) over the controller: the left hand must go
///      where the clip puts it while everything else stays, frame for frame, exactly where the twin has
///      it; then the masked layer is stopped and the arm must be the twin's again;
///   3. a walking loop is played and its speed matched to a ground speed: the planted foot must go back
///      under the body at that speed (so it would not slide on the ground), at the speed the clip was made
///      for and at half of it;
///   4. afterwards the player has no graph and its stepping component is switched off.
/// </summary>
public sealed class CrossFadeProbe : MonoBehaviour
{
    public const string WantedKey = "AnimProve.Wanted";
    public const string BundleKey = "AnimProve.Bundle";
    public const string RigKey = "AnimProve.Rig";
    public const string ControllerPath = "Assets/Generated/StandIn.controller";

    private const string Tag = "[AnimBundle] ";
    private const string Standing = "direct_traffic";
    private const string Kneeling = "kneel_work";
    private const string Tooled = "sweep";
    private const string Masked = "job_flyer_hold";
    private const string Walking = "push_cart";
    private const float WalkingSpeed = 1.2f;

    private Animator _animator, _twin;
    private Transform _hips;
    private ActivityAnimator _player;
    private ActivityAnimators _service;
    private AssetBundle _bundle;
    private readonly Dictionary<string, AnimationClip> _clips = new Dictionary<string, AnimationClip>();
    private readonly List<float> _heights = new List<float>(256);
    private RuntimeAnimatorController _controller;
    private Vector3 _root;
    private float _start;
    private int _step;
    private float _before, _half, _during, _after, _worstGrip, _fadeStart;
    private bool _monotonic = true;
    private HeldTool _held;
    private GameObject _tool;
    private ToolGrip _grip;
    private int _framesCostFree;

    // The masked layer.
    private static readonly HumanBodyBones[] Untouched =
    {
        HumanBodyBones.Hips, HumanBodyBones.Head, HumanBodyBones.RightHand, HumanBodyBones.RightLowerArm, HumanBodyBones.LeftFoot, HumanBodyBones.RightFoot,
        HumanBodyBones.LeftLowerLeg, HumanBodyBones.RightLowerLeg, HumanBodyBones.UpperChest
    };
    private float _maskedOthers, _maskedHand, _maskedAfter, _twinDrift;
    private int _maskedFrames;
    private float _worstApart;
    private string _worstBone = "";

    // The walking speed.
    private readonly float[] _footSpeed = new float[2];
    private readonly int[] _footFrames = new int[2];
    private float _lastFoot;
    private bool _haveLastFoot;
    private float _playedAt;

#if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Begin()
    {
        if (!SessionState.GetBool(WantedKey, false))
        {
            return;
        }
        SessionState.SetBool(WantedKey, false);
        new GameObject("CrossFadeProbe").AddComponent<CrossFadeProbe>();
    }

    private void Start()
    {
        try
        {
            _bundle = AssetBundle.LoadFromFile(SessionState.GetString(BundleKey, ""));
            Check(_bundle != null, "the bundle could not be loaded in play mode");
            TextAsset catalogue = null;
            foreach (UnityEngine.Object asset in _bundle.LoadAllAssets())
            {
                if (asset is AnimationClip clip) { _clips[clip.name] = clip; }
                else if (asset is TextAsset text) { catalogue = text; }
            }
            Check(_clips.ContainsKey(Standing) && _clips.ContainsKey(Kneeling), "the proof needs the clips " + Standing + " and " + Kneeling);
            Check(_clips.ContainsKey(Masked) && _clips.ContainsKey(Walking), "the proof needs the clips " + Masked + " and " + Walking);
            _controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ControllerPath);
            Check(_controller != null, "no controller at " + ControllerPath);

            string rig = File.ReadAllText(SessionState.GetString(RigKey, ""));
            _animator = StandInRig.Build(rig);
            _animator.runtimeAnimatorController = _controller;
            _twin = StandInRig.Build(rig);
            _twin.name = "Twin";
            _twin.runtimeAnimatorController = _controller;
            _hips = _animator.GetBoneTransform(HumanBodyBones.Hips);
            _root = _animator.transform.position;
            _service = gameObject.AddComponent<ActivityAnimators>();
            _player = ActivityAnimators.For(_animator);
            Check(_player.Usable, "the player says the stand-in cannot be animated");
            if (_clips.ContainsKey(Tooled) && catalogue != null)
            {
                var rows = JsonUtility.FromJson<Rows>(catalogue.text);
                foreach (Row row in rows.clips)
                {
                    if (row.name == Tooled && row.tool != null && row.tool.otherHand != null && row.tool.otherHand.wristOnTool != null && row.tool.otherHand.wristOnTool.Length == 3)
                    {
                        _grip = new ToolGrip
                        {
                            Hand = row.tool.hand == "Left" ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand,
                            Position = new Vector3(row.tool.position[0], row.tool.position[1], row.tool.position[2]),
                            Rotation = new Quaternion(row.tool.rotation[0], row.tool.rotation[1], row.tool.rotation[2], row.tool.rotation[3]),
                            TwoHanded = true,
                            OtherHand = row.tool.otherHand.hand == "Left" ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand,
                            WristOnTool = new Vector3(row.tool.otherHand.wristOnTool[0], row.tool.otherHand.wristOnTool[1], row.tool.otherHand.wristOnTool[2])
                        };
                        _tool = new GameObject("Tool");
                        _held = new HeldTool();
                    }
                }
            }
            _start = Time.time;
        }
        catch (Exception e)
        {
            Fail(e.Message);
        }
    }

    // After the Animator has been evaluated: what a camera would see this frame.
    private void LateUpdate()
    {
        if (_animator == null)
        {
            return;
        }
        try
        {
            float t = Time.time - _start;
            float hips = _hips.position.y;
            switch (_step)
            {
                case 0:     // the controller alone: the two stand-ins must be the same
                    if (t > 0.3f)
                    {
                        _twinDrift = Mathf.Max(_twinDrift, Apart(Untouched));
                    }
                    if (t > 0.6f)
                    {
                        _before = hips;
                        Check(_player.Play(_clips[Kneeling], 0.5f), "Play returned false");
                        _fadeStart = Time.time;
                        _heights.Clear();
                        _step = 1;
                    }
                    break;
                case 1:     // fading in
                    if (_heights.Count > 0 && hips > _heights[_heights.Count - 1] + 0.004f)
                    {
                        _monotonic = false;
                    }
                    _heights.Add(hips);
                    if (_half == 0f && Time.time - _fadeStart >= 0.25f)
                    {
                        _half = hips;
                    }
                    if (Time.time - _fadeStart > 1.0f)
                    {
                        _during = hips;
                        Check(_player.Weight > 0.999f, "the fade in did not finish");
                        _step = 2;
                        if (_held != null)
                        {
                            Check(_player.Play(_clips[Tooled], 0.3f), "the second clip did not play");
                            Check(_held.Attach(_animator, _tool.transform, _grip), "the tool could not be put in the hand");
                            _fadeStart = Time.time;
                        }
                    }
                    break;
                case 2:     // a second clip, with a tool in both hands
                    if (_held != null && Time.time - _fadeStart > 0.5f)
                    {
                        Transform wrist = _animator.GetBoneTransform(_grip.OtherHand);
                        _worstGrip = Mathf.Max(_worstGrip, (wrist.position - _tool.transform.TransformPoint(_grip.WristOnTool)).magnitude);
                    }
                    if (_held == null || Time.time - _fadeStart > 1.6f)
                    {
                        if (_held != null)
                        {
                            _held.Detach(null);
                        }
                        _player.Stop(0.5f);
                        _fadeStart = Time.time;
                        _step = 3;
                    }
                    break;
                case 3:     // handed back
                    if (Time.time - _fadeStart > 1.0f)
                    {
                        _after = hips;
                        Check(!_player.Active, "the graph was not destroyed after the fade out");
                        Check(_player.PlayOver(_clips[Masked], BodyMask.LeftArm, 0.3f), "PlayOver returned false");
                        _fadeStart = Time.time;
                        _step = 4;
                    }
                    break;
                case 4:     // the masked layer over the controller: only the left arm is ours
                    if (Time.time - _fadeStart > 0.6f)
                    {
                        _maskedOthers = Mathf.Max(_maskedOthers, Apart(Untouched));
                        _maskedHand += HandFromChest(_animator, _twin);
                        _maskedFrames++;
                    }
                    if (Time.time - _fadeStart > 1.8f)
                    {
                        Check(_player.OverWeight > 0.999f && _player.Weight <= 0f, "the masked layer did not fade in on its own");
                        _player.StopOver(0.3f);
                        _fadeStart = Time.time;
                        _step = 5;
                    }
                    break;
                case 5:     // the masked layer stopped: the arm is the controller's again
                    if (Time.time - _fadeStart > 0.8f)
                    {
                        _maskedAfter = HandFromChest(_animator, _twin);
                        Check(!_player.Active, "the graph was not destroyed after the masked layer stopped");
                        Check(_player.Play(_clips[Walking], 0.2f), "the walking clip did not play");
                        _fadeStart = Time.time;
                        _playedAt = 1f;
                        _haveLastFoot = false;
                        _step = 6;
                    }
                    break;
                case 6:     // walking at the speed the clip was made for
                case 7:     // and at half of it
                    if (Time.time - _fadeStart > 0.5f)
                    {
                        PlantedFoot(_step - 6);
                    }
                    if (Time.time - _fadeStart > 3.0f)
                    {
                        if (_step == 6)
                        {
                            _playedAt = _player.MatchSpeed(WalkingSpeed * 0.5f, WalkingSpeed);
                            _fadeStart = Time.time;
                            _haveLastFoot = false;
                            _step = 7;
                        }
                        else
                        {
                            _player.Stop(0.3f);
                            _fadeStart = Time.time;
                            _step = 8;
                        }
                    }
                    break;
                case 8:
                    if (Time.time - _fadeStart > 0.8f)
                    {
                        _framesCostFree = 0;
                        _step = 9;
                    }
                    break;
                case 9:     // a few more frames: nothing of ours may be running
                    _framesCostFree++;
                    if (_framesCostFree > 5)
                    {
                        Report();
                    }
                    break;
            }
            if (Time.realtimeSinceStartup > 240f)
            {
                Fail("timed out in step " + _step);
            }
        }
        catch (Exception e)
        {
            Fail(e.Message);
        }
    }

    // How far the given bones of the stand-in are from the twin's, at worst.
    private float Apart(HumanBodyBones[] bones)
    {
        float worst = 0f;
        Vector3 offset = _twin.transform.position - _animator.transform.position;
        foreach (HumanBodyBones bone in bones)
        {
            float apart = (_animator.GetBoneTransform(bone).position + offset - _twin.GetBoneTransform(bone).position).magnitude;
            if (apart > _worstApart)
            {
                _worstApart = apart;
                _worstBone = bone.ToString();
            }
            worst = Mathf.Max(worst, apart);
        }
        return worst;
    }

    // How far apart the two left hands are when each is measured from its own chest.
    private static float HandFromChest(Animator a, Animator b)
    {
        Vector3 mine = a.GetBoneTransform(HumanBodyBones.UpperChest).InverseTransformPoint(a.GetBoneTransform(HumanBodyBones.LeftHand).position);
        Vector3 theirs = b.GetBoneTransform(HumanBodyBones.UpperChest).InverseTransformPoint(b.GetBoneTransform(HumanBodyBones.LeftHand).position);
        return (mine - theirs).magnitude;
    }

    // The speed at which a foot that is on the ground goes back under the body.
    private void PlantedFoot(int phase)
    {
        Transform foot = _animator.GetBoneTransform(HumanBodyBones.LeftFoot);
        float along = foot.position.z;
        if (_haveLastFoot && Time.deltaTime > 0f && foot.position.y < 0.129f)
        {
            float speed = (_lastFoot - along) / Time.deltaTime;
            if (speed > 0.05f)
            {
                _footSpeed[phase] += speed;
                _footFrames[phase]++;
            }
        }
        _lastFoot = along;
        _haveLastFoot = true;
    }

    private void Report()
    {
        float moved = (_animator.transform.position - _root).magnitude;
        float hand = _maskedFrames > 0 ? _maskedHand / _maskedFrames : 0f;
        float full = _footFrames[0] > 0 ? _footSpeed[0] / _footFrames[0] : 0f;
        float halfSpeed = _footFrames[1] > 0 ? _footSpeed[1] / _footFrames[1] : 0f;
        Debug.Log(Tag + "play mode, cross-fade over an Animator Controller: hips " + _before.ToString("F3") + " m (controller) -> " + _half.ToString("F3") + " (half way) -> "
                  + _during.ToString("F3") + " (clip) -> " + _after.ToString("F3") + " (handed back) over " + _heights.Count + " frames; root moved " + moved.ToString("F4")
                  + " m; second hand " + (_worstGrip * 1000f).ToString("F1") + " mm off the tool at worst; afterwards graph alive: " + _player.Active + ", stepping component enabled: " + _service.enabled);
        Debug.Log(Tag + "play mode, masked layer (" + Masked + ", left arm) over the controller: the left hand moved " + (hand * 100f).ToString("F1") + " cm from where the controller has it; hips, head, right arm, chest and both legs stayed within "
                  + (_maskedOthers * 1000f).ToString("F2") + " mm (" + _worstBone + ") of a twin running the controller alone over " + _maskedFrames + " frames (the two were " + (_twinDrift * 1000f).ToString("F2")
                  + " mm apart before); after StopOver the hand is " + (_maskedAfter * 1000f).ToString("F1") + " mm from the twin's");
        Debug.Log(Tag + "play mode, speed matching (" + Walking + ", made for " + WalkingSpeed.ToString("F1") + " m/s): planted foot goes back at " + full.ToString("F2") + " m/s at speed 1 and at " + halfSpeed.ToString("F2")
                  + " m/s after MatchSpeed(" + (WalkingSpeed * 0.5f).ToString("F1") + ", " + WalkingSpeed.ToString("F1") + ") = " + _playedAt.ToString("F2"));
        Check(_before > 0.8f, "the controller's standing pose is not standing (hips " + _before.ToString("F2") + " m)");
        Check(_during < 0.6f, "the clip did not take the character over (hips " + _during.ToString("F2") + " m)");
        Check(_half < _before - 0.03f && _half > _during + 0.03f, "the cross-fade did not pass through a pose in between");
        Check(_monotonic, "the hips did not go down steadily during the fade in");
        Check(Mathf.Abs(_after - _before) < 0.03f, "the character was not handed back to the controller (hips " + _after.ToString("F2") + " m)");
        Check(_animator.runtimeAnimatorController == _controller, "the controller was replaced");
        Check(moved < 0.001f, "the clips moved the character's root");
        Check(_held == null || _worstGrip < 0.012f, "the second hand came off the tool");
        Check(_twinDrift < 0.002f, "the two stand-ins do not run the controller alike, so the masked layer cannot be judged");
        Check(hand > 0.15f, "the masked layer did not take the left arm (the hand is where the controller has it)");
        Check(_maskedOthers < 0.012f, "the masked layer disturbed the rest of the body (" + (_maskedOthers * 1000f).ToString("F1") + " mm)");
        Check(_maskedAfter < 0.004f, "the left arm was not handed back after StopOver");
        Check(Mathf.Abs(full - WalkingSpeed) < 0.12f, "the walking clip's planted foot does not go back at the speed the clip is listed for");
        Check(Mathf.Abs(halfSpeed - WalkingSpeed * 0.5f) < 0.08f, "MatchSpeed did not bring the planted foot to the ground speed asked for");
        Check(!_player.Active, "the graph was not destroyed after the fade out");
        Check(!_service.enabled, "the stepping component is still running with nothing to do");
        Debug.Log(Tag + "PROVED: bundled Humanoid clips play on the game's skeleton through a playable graph layered over its controller, a masked clip takes one arm and nothing else, a walking clip keeps step with the ground speed it is matched to, and everything hands back cleanly");
        Finish(0);
    }

    private void Fail(string problem)
    {
        Debug.LogError(Tag + "FAILED: " + problem);
        Finish(1);
    }

    private void Finish(int code)
    {
        _animator = null;
        ActivityAnimators.ReleaseAll();
        if (_bundle != null)
        {
            _bundle.Unload(true);
        }
        EditorApplication.Exit(code);
    }
#endif

    private static void Check(bool condition, string problem)
    {
        if (!condition)
        {
            throw new Exception(problem);
        }
    }

    [Serializable]
    private class Rows
    {
        public Row[] clips;
    }

    [Serializable]
    private class Row
    {
        public string name;
        public AnimBundleTool tool;
    }
}

[Serializable]
public class AnimBundleTool
{
    public string prop;
    public string hand;
    public float[] position;
    public float[] rotation;
    public AnimBundleOtherHand otherHand;
}

[Serializable]
public class AnimBundleOtherHand
{
    public string hand;
    public float[] wristOnTool;
}
