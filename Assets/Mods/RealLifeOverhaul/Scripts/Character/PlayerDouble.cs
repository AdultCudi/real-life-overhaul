using System;
using System.Collections.Generic;
using BAModAPI;
using BigAmbitions.Characters.Appearance;
using Helpers;
using UnityEngine;
using UnityEngine.Rendering;

namespace RealLifeOverhaul
{
    /// <summary>
    /// Shows the player in the city with the game's detailed character set.
    ///
    /// The game ships two complete character sets with the same clothes, hair and faces. Everyone in
    /// the city, the player included, uses the light one, made for a camera high above the street.
    /// The detailed one is only used in character creation and the shop previews. With this mod's
    /// close camera the light one looks coarse, so this component keeps a "double": a copy of a
    /// detailed preview character, dressed by the game's own code from a copy of the player's
    /// appearance data, posed every frame from the player's skeleton, and drawn instead of the
    /// player's own body. The player object itself (movement, animation, collisions, held items,
    /// the shadow) is untouched and the savegame is only read.
    ///
    /// Per frame: one copy of about forty bone rotations from array to array and two visibility
    /// checks. Everything else happens when the player's look changes.
    ///
    /// If anything is not as expected the player's own body is shown again and the feature stays
    /// off until the next load. It never leaves the player invisible.
    /// </summary>
    [DefaultExecutionOrder(10000)]   // after the game's own LateUpdate work on the player's bones
    public class PlayerDouble : MonoBehaviour
    {
        private const float RetrySeconds = 1f;
        private const int MinMatchedBones = 24;       // of the 40 in the city skeleton
        private const float MaxBoneGap = 0.25f;       // metres between a copied joint and the player's, at player scale 1
        private const float MaxLegRatioOff = 0.15f;

        private enum State { Waiting, Active }

        public static IModLogger Logger;
        public static PlayerDouble Instance { get; private set; }

        private State _state = State.Waiting;
        private bool _failed;
        private float _nextTry;

        // The player.
        private PlayerController _player;
        private GameObject _playerObject;
        private Transform _playerRoot;
        private AppearanceSetter _playerLook;
        private SkinnedMeshRenderer _playerHead;
        private Animator _playerAnimator;
        private AnimatorCullingMode _playerCulling;
        private Action _onPlayerLookChanged;
        private bool _lookChanged;
        private readonly List<Renderer> _hiddenLow = new List<Renderer>(12);

        // The double.
        private string _sourceName;
        private GameObject _holder;
        private GameObject _doubleObject;
        private Transform _doubleRoot;
        private AppearanceSetter _doubleLook;
        private bool _shown;
        private bool _suspended;      // the current outfit has a piece the detailed set lacks; the player's own body is shown
        private bool _suspendWarned;

        // The pose copy.
        private Transform[] _from = Array.Empty<Transform>();
        private Transform[] _to = Array.Empty<Transform>();
        // Fingers the player's hand does not have (middle, little) follow the one beside them that it does (index, ring).
        private Transform[] _fingerFrom = Array.Empty<Transform>();
        private Transform[] _fingerTo = Array.Empty<Transform>();
        private Quaternion[] _fingerOffset = Array.Empty<Quaternion>();
        private Transform _fromHips, _toHips, _fromArmature, _toArmature;
        private float _hipsScale = 1f;
        private Vector3 _appliedScale;
        private bool _verified;
        private float _worstGap;
        private string _worstBone = "";

        private void Awake()
        {
            Instance = this;
            _onPlayerLookChanged = OnPlayerLookChanged;
        }

        private void OnDestroy()
        {
            try
            {
                TearDown();
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
            if (Instance == this)
            {
                Instance = null;
            }
        }

        // Called from inside the game's own appearance code: note it and leave. The work is done in LateUpdate.
        private void OnPlayerLookChanged()
        {
            _lookChanged = true;
        }

        private void LateUpdate()
        {
            try
            {
                Tick();
            }
            catch (Exception e)
            {
                Fail("an unexpected error", e);
            }
        }

        private void Tick()
        {
            if (!Settings.CharacterHighDetail)
            {
                if (_state == State.Active)
                {
                    TearDown();
                    Logger?.Info("High-detail player: switched off, the player's own body is shown.");
                }
                return;
            }

            if (_state == State.Waiting)
            {
                if (Time.unscaledTime < _nextTry)
                {
                    return;
                }
                _nextTry = Time.unscaledTime + RetrySeconds;
                TryStart();
                if (_state != State.Active)
                {
                    return;
                }
                // Carry on: the double is posed in the same frame the player's own body stops being drawn.
            }

            // The player, its head or the double was destroyed or replaced: start again from nothing.
            if (_player == null || _playerHead == null || _doubleObject == null)
            {
                TearDown();
                return;
            }

            if (_lookChanged)
            {
                _lookChanged = false;
                Dress();
                if (_state != State.Active)
                {
                    return;
                }
            }
            if (_suspended)
            {
                return;
            }
            if (_toHips == null || _fromHips == null || _toArmature == null || _fromArmature == null)
            {
                TearDown();
                return;
            }

            // Wherever the game hides the player (vehicles, some interiors) it switches these off.
            bool show = _playerHead.enabled && _playerObject.activeInHierarchy;
            if (show != _shown)
            {
                _shown = show;
                _doubleObject.SetActive(show);
            }
            if (!show)
            {
                return;
            }

            CopyPose();
            if (!_verified)
            {
                Verify();
            }
        }

        // The whole per-frame cost of the feature.
        private void CopyPose()
        {
            Vector3 scale = _playerRoot.lossyScale;
            if (scale != _appliedScale)
            {
                _appliedScale = scale;
                _doubleRoot.localScale = scale;
            }
            _doubleRoot.SetPositionAndRotation(_playerRoot.position, _playerRoot.rotation);
            _toArmature.SetPositionAndRotation(_fromArmature.position, _fromArmature.rotation);
            _toHips.localPosition = _fromHips.localPosition * _hipsScale;

            Transform[] from = _from;
            Transform[] to = _to;
            for (int i = 0; i < from.Length; i++)
            {
                to[i].localRotation = from[i].localRotation;
            }
            Transform[] fingerFrom = _fingerFrom;
            for (int i = 0; i < fingerFrom.Length; i++)
            {
                _fingerTo[i].localRotation = _fingerOffset[i] * fingerFrom[i].localRotation;
            }
        }

        // ---------------------------------------------------------------- start

        private void TryStart()
        {
            PlayerController player = PlayerHelper.PlayerController;
            if (player == null || player.Character == null || player.Character.appearanceSetter == null)
            {
                return;   // still loading
            }
            AppearanceSetter look = player.Character.appearanceSetter;
            if (look.data == null || look.data.elements == null || look.data.elements.Count == 0
                || look.HeadMesh == null || look.GetSelectedMeshRenderers().Count == 0)
            {
                return;   // not dressed yet
            }
            if (CharacterTools.IsHighDetail(look))
            {
                Fail("the player already uses a detailed character", null);
                return;
            }

            AppearanceSetter source = CharacterTools.FindHighSource(look, out string searched);
            if (source == null)
            {
                Fail("no detailed character was found in the city scene to copy. " + searched, null);
                return;
            }

            _player = player;
            _playerObject = player.gameObject;
            _playerRoot = player.transform;
            _playerLook = look;
            _playerHead = look.HeadMesh;
            _sourceName = source.name;

            Build(source);

            _playerAnimator = player.Character.animator;
            if (_playerAnimator != null)
            {
                // With its own body not drawn, the player's animator would count as off screen and stop.
                _playerCulling = _playerAnimator.cullingMode;
                _playerAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            }
            look.visualsUpdated += _onPlayerLookChanged;
            _lookChanged = false;
            _state = State.Active;

            Dress();
            if (_state == State.Active)
            {
                Logger?.Info("High-detail player: active, copied from '" + _sourceName + "', " + _from.Length + " bones follow the player"
                             + (_suspended ? " (waiting: the current outfit is not in the detailed set)." : "."));
            }
        }

        private void Build(AppearanceSetter source)
        {
            // The copy must not carry over anything the hair styling did to the source.
            CharacterQuality quality = CharacterQuality.Instance;
            if (quality != null)
            {
                quality.SuspendHair();
            }

            try
            {
                // Copied into an inactive parent, so that none of the scripts on the copy wake up before they are removed.
                // The parent belongs to the mod, not to the player: the game's searches of the player's children never see the copy.
                _holder = new GameObject("RealLifeOverhaul Player Double");
                _holder.SetActive(false);
                _holder.transform.SetParent(transform, false);

                _doubleObject = Instantiate(source.gameObject, _holder.transform);
                _doubleObject.name = "RealLifeOverhaul High Detail Player";
                _doubleRoot = _doubleObject.transform;
                _doubleRoot.localPosition = Vector3.zero;
                _doubleRoot.localRotation = Quaternion.identity;
                _doubleRoot.localScale = Vector3.one;
                _doubleLook = _doubleObject.GetComponent<AppearanceSetter>();

                CharacterTools.Strip(_doubleObject, KeepOnDouble, false);
                foreach (Animator animator in _doubleObject.GetComponentsInChildren<Animator>(true))
                {
                    animator.enabled = false;   // kept only because the game's appearance code sets a value on it
                }
                foreach (SkinnedMeshRenderer part in _doubleObject.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    ClearShapes(part);   // whatever face the preview character last wore
                }
                _doubleLook.skipMeshMerging = true;
                _doubleLook.meshCombiner = null;
                _doubleLook.visualsUpdated = null;
                CharacterTools.SetLayer(_doubleRoot, _playerHead.gameObject.layer);

                _doubleObject.SetActive(true);
                _holder.SetActive(true);
                _shown = true;
                _appliedScale = Vector3.one;
            }
            finally
            {
                if (quality != null)
                {
                    quality.ResumeHair();
                }
            }
            if (quality != null)
            {
                quality.Manage(_doubleLook);   // hair styling and sharper textures, on every change of look
            }
        }

        private static bool KeepOnDouble(MonoBehaviour script)
        {
            return script is AppearanceSetter || script is AppearanceElementVariant || script is BlendshapeWrapper;
        }

        // ---------------------------------------------------------------- dressing

        /// <summary>Gives the double the player's current look and hides the player's own body.</summary>
        private void Dress()
        {
            _playerHead = _playerLook.HeadMesh;
            if (_playerHead == null)
            {
                Fail("the player has no head mesh", null);
                return;
            }

            // A copy: the game's appearance code keeps the object it is given, and the player's is the savegame's.
            CharacterData copy = _playerLook.data.Copy();
            _doubleObject.SetActive(true);
            _shown = true;

            // The game only writes the face shapes the data lists. Clear what the copied preview
            // character (or the previous look) left behind, or the player inherits someone else's nose.
            IReadOnlyList<SkinnedMeshRenderer> parts = _doubleLook.GetSelectedMeshRenderers();
            for (int i = 0; i < parts.Count; i++)
            {
                ClearShapes(parts[i]);
            }
            ClearShapes(_doubleLook.maleHeadMesh);
            ClearShapes(_doubleLook.femaleHeadMesh);

            _doubleLook.SetAppearance(copy);

            SkinnedMeshRenderer head = _doubleLook.HeadMesh;
            parts = _doubleLook.GetSelectedMeshRenderers();
            if (head == null || parts.Count == 0)
            {
                Fail("the detailed character came out empty", null);
                return;
            }
            // Lit like the player; the player's own shadow mesh stays the only thing casting a shadow.
            uint lightLayers = _playerHead.renderingLayerMask;
            for (int i = 0; i < parts.Count; i++)
            {
                Finish(parts[i], lightLayers);
            }
            Finish(head, lightLayers);
            ModLooks.SetDouble(_doubleLook, lightLayers);   // the mod's own hair and clothes: same lighting, and they stay in place of the game's

            string missing = MissingPieces();
            _suspended = missing != null;
            if (_suspended)
            {
                if (!_suspendWarned)
                {
                    _suspendWarned = true;
                    Logger?.Warn("High-detail player: the detailed set has no match for " + missing + "; the player's own body is shown while that is worn. (Logged once.)");
                }
                RestoreLow();
                _doubleObject.SetActive(false);
                _shown = false;
                return;
            }

            if (!Bind(head))
            {
                return;
            }
            HideLow();
        }

        private static void ClearShapes(SkinnedMeshRenderer part)
        {
            if (part == null || part.sharedMesh == null)
            {
                return;
            }
            int shapes = part.sharedMesh.blendShapeCount;
            for (int i = 0; i < shapes; i++)
            {
                part.SetBlendShapeWeight(i, 0f);
            }
        }

        private static void Finish(SkinnedMeshRenderer part, uint lightLayers)
        {
            if (part != null)
            {
                part.shadowCastingMode = ShadowCastingMode.Off;
                part.renderingLayerMask = lightLayers;
                part.forceRenderingOff = false;
                part.enabled = true;
            }
        }

        // Every piece the player is visibly wearing must exist in the detailed set under the same id.
        private string MissingPieces()
        {
            string missing = null;
            List<AppearanceElementData> worn = _playerLook.data.elements;
            for (int i = 0; i < worn.Count; i++)
            {
                AppearanceElementData piece = worn[i];
                if (piece == null || string.IsNullOrEmpty(piece.variantId) || !IsWornPiece(piece.type))
                {
                    continue;
                }
                if (HasVisibleVariant(_playerLook, piece) && !HasVisibleVariant(_doubleLook, piece))
                {
                    missing = (missing == null ? "" : missing + ", ") + piece.type + " '" + piece.variantId + "'";
                }
            }
            return missing;
        }

        // The pieces the game's UpdateVisuals switches on as meshes. Head shape, eyes, mouth and nose are
        // blendshapes, and eyebrows are a mesh in one set and a material in the other.
        private static bool IsWornPiece(AppearanceElementType type)
        {
            switch (type)
            {
                case AppearanceElementType.Hair:
                case AppearanceElementType.HairAccessory:
                case AppearanceElementType.Beard:
                case AppearanceElementType.HeadAccessory:
                case AppearanceElementType.Torso:
                case AppearanceElementType.TorsoAccessory:
                case AppearanceElementType.Legs:
                case AppearanceElementType.LegsAccessory:
                case AppearanceElementType.Feet:
                case AppearanceElementType.FeetAccessory:
                    return true;
                default:
                    return false;
            }
        }

        private static bool HasVisibleVariant(AppearanceSetter look, AppearanceElementData piece)
        {
            List<AppearanceElementVariant> variants = look.GetElementVariants(piece.type);
            if (variants == null)
            {
                return false;
            }
            for (int i = 0; i < variants.Count; i++)
            {
                if (variants[i].id == piece.variantId)
                {
                    return variants[i].GetComponent<SkinnedMeshRenderer>() != null;
                }
            }
            return false;
        }

        // ---------------------------------------------------------------- the player's own body

        private void HideLow()
        {
            RestoreLow();
            IReadOnlyList<SkinnedMeshRenderer> parts = _playerLook.GetSelectedMeshRenderers();
            for (int i = 0; i < parts.Count; i++)
            {
                Hide(parts[i]);
            }
            Hide(_playerHead);
            Hide(_playerLook.lastMergedMeshRenderer);
        }

        // Not drawn, but still "enabled" as far as the game is concerned: its own show and hide keep working.
        private void Hide(Renderer part)
        {
            if (part != null && !part.forceRenderingOff)
            {
                part.forceRenderingOff = true;
                _hiddenLow.Add(part);
            }
        }

        private void RestoreLow()
        {
            for (int i = 0; i < _hiddenLow.Count; i++)
            {
                if (_hiddenLow[i] != null)
                {
                    _hiddenLow[i].forceRenderingOff = false;
                }
            }
            _hiddenLow.Clear();
        }

        // ---------------------------------------------------------------- skeleton

        /// <summary>Pairs the two skeletons by bone name, once. Bones only the detailed one has (fingers, eyes, jaw) are given a fixed pose.</summary>
        private bool Bind(SkinnedMeshRenderer doubleHead)
        {
            Transform fromHips = FindHips(_playerHead);
            Transform toHips = FindHips(doubleHead);
            if (fromHips == null || toHips == null || fromHips.parent == null || toHips.parent == null)
            {
                Fail("a skeleton has no hips bone", null);
                return false;
            }
            if (fromHips == _fromHips && toHips == _toHips && _from.Length > 0)
            {
                return true;   // same skeletons as before
            }

            var byName = new Dictionary<string, Transform>(64);
            foreach (Transform bone in fromHips.GetComponentsInChildren<Transform>(true))
            {
                if (!byName.ContainsKey(bone.name))
                {
                    byName.Add(bone.name, bone);
                }
            }
            var from = new List<Transform>(64);
            var to = new List<Transform>(64);
            foreach (Transform bone in toHips.GetComponentsInChildren<Transform>(true))
            {
                if (byName.TryGetValue(bone.name, out Transform match))
                {
                    from.Add(match);
                    to.Add(bone);
                }
            }
            if (from.Count < MinMatchedBones)
            {
                Fail("only " + from.Count + " bones of the two skeletons have the same name", null);
                return false;
            }

            _fromHips = fromHips;
            _toHips = toHips;
            _fromArmature = fromHips.parent;
            _toArmature = toHips.parent;
            _from = from.ToArray();
            _to = to.ToArray();
            PoseUnmatchedBones(doubleHead, byName);

            // Hips height is copied as a position, so it is rescaled if the two rigs are built at a
            // different scale or with slightly different leg lengths; otherwise the feet float or sink.
            // Both measured relative to their own character root, so the player's scale does not come into it.
            float fromRoot = Mathf.Max(1e-5f, _playerRoot.lossyScale.y);
            float toRoot = Mathf.Max(1e-5f, _doubleRoot.lossyScale.y);
            float fromUnit = Mathf.Max(1e-5f, _fromArmature.lossyScale.y / fromRoot);
            float toUnit = Mathf.Max(1e-5f, _toArmature.lossyScale.y / toRoot);
            float legs = 1f;
            float fromLeg = LegLength(fromHips) / fromRoot;
            float toLeg = LegLength(toHips) / toRoot;
            if (fromLeg > 0.1f && toLeg > 0.1f && Mathf.Abs(toLeg / fromLeg - 1f) <= MaxLegRatioOff)
            {
                legs = toLeg / fromLeg;
            }
            _hipsScale = fromUnit / toUnit * legs;
            _verified = false;
            _worstGap = 0f;
            _worstBone = "";
            return true;
        }

        /// <summary>
        /// The bones only the detailed skeleton has. The player's hand is a simpler one: a thumb, an index
        /// finger that stands for index and middle, and a ring finger that stands for ring and little. The
        /// game animates those (open when idle, a fist when running), so here the middle finger is made to
        /// follow the index and the little finger the ring, each from its own rest pose: without that, two
        /// fingers close and two stay out, which is a claw. Bones with nothing to follow (jaw, eyes, or a
        /// finger whose neighbour is missing) are put, once, in their rest pose, the fingers with the
        /// game's idle hand on top. Rest poses come from the head mesh's bind poses, so they do not depend
        /// on the pose the copied character happened to be in.
        /// </summary>
        private void PoseUnmatchedBones(SkinnedMeshRenderer doubleHead, Dictionary<string, Transform> onPlayer)
        {
            _fingerFrom = Array.Empty<Transform>();
            _fingerTo = Array.Empty<Transform>();
            _fingerOffset = Array.Empty<Quaternion>();
            Mesh mesh = doubleHead.sharedMesh;
            Transform[] bones = doubleHead.bones;
            Matrix4x4[] rest = mesh != null ? mesh.bindposes : null;
            if (rest == null || rest.Length != bones.Length)
            {
                return;
            }
            var followFrom = new List<Transform>(12);
            var followTo = new List<Transform>(12);
            var followOffset = new List<Quaternion>(12);
            for (int i = 0; i < bones.Length; i++)
            {
                Transform bone = bones[i];
                if (bone == null || bone.parent == null || onPlayer.ContainsKey(bone.name))
                {
                    continue;
                }
                int parent = Array.IndexOf(bones, bone.parent);
                if (parent < 0)
                {
                    continue;
                }
                // A bind pose takes mesh space to bone space, so parent * inverse(child) is the child's rest pose in its parent.
                Quaternion restRotation = (rest[parent] * rest[i].inverse).rotation;

                string leaderName = Leader(bone.name);
                int leader = leaderName != null ? IndexOfBone(bones, leaderName) : -1;
                int leaderParent = leader >= 0 && bones[leader].parent != null ? Array.IndexOf(bones, bones[leader].parent) : -1;
                if (leaderParent >= 0 && onPlayer.TryGetValue(leaderName, out Transform driver))
                {
                    // follower = its rest * (what the leader has on top of the leader's rest)
                    Quaternion leaderRest = (rest[leaderParent] * rest[leader].inverse).rotation;
                    followFrom.Add(driver);
                    followTo.Add(bone);
                    followOffset.Add(restRotation * Quaternion.Inverse(leaderRest));
                }
                else
                {
                    bone.localRotation = restRotation * FingerCurl(bone.name);
                }
            }
            _fingerFrom = followFrom.ToArray();
            _fingerTo = followTo.ToArray();
            _fingerOffset = followOffset.ToArray();
        }

        // The finger of the player's simpler hand that a finger of the detailed hand moves with, joint for joint.
        private static string Leader(string bone)
        {
            if (bone.StartsWith("MidFinger", StringComparison.Ordinal))
            {
                return "IndexFinger" + bone.Substring("MidFinger".Length);
            }
            if (bone.StartsWith("PinkyFinger", StringComparison.Ordinal))
            {
                return "RingFinger" + bone.Substring("PinkyFinger".Length);
            }
            return null;
        }

        private static int IndexOfBone(Transform[] bones, string name)
        {
            for (int i = 0; i < bones.Length; i++)
            {
                if (bones[i] != null && bones[i].name == name)
                {
                    return i;
                }
            }
            return -1;
        }

        // The pose the game's own idle gives the detailed hands in character creation, as the rotation each joint of the
        // LEFT hand has on top of its rest pose (x, y, z, w), knuckle to tip. Measured from the game, not authored:
        // about 6, 13 and 14 degrees of curl on the fingers. The right hand is the mirror image: y and z negated.
        private static readonly string[] FingerNames = { "IndexFinger", "MidFinger", "RingFinger", "PinkyFinger", "ThumbFinger" };
        private static readonly Quaternion[] IdleHand =
        {
            new Quaternion(-0.0459f, -0.0043f, -0.0349f, 0.9983f), new Quaternion(-0.1089f, 0.0076f, 0.0140f, 0.9939f), new Quaternion(-0.1222f, 0.0118f, 0.0151f, 0.9923f),
            new Quaternion(-0.0456f, -0.0204f, -0.0108f, 0.9987f), new Quaternion(-0.1121f, -0.0039f, 0.0033f, 0.9937f), new Quaternion(-0.1243f, 0.0015f, 0.0036f, 0.9922f),
            new Quaternion(-0.0451f, -0.0150f, 0.0282f, 0.9985f), new Quaternion(-0.1109f, -0.0026f, -0.0118f, 0.9938f), new Quaternion(-0.1242f, -0.0018f, 0.0027f, 0.9923f),
            new Quaternion(-0.0440f, -0.0079f, 0.0790f, 0.9959f), new Quaternion(-0.1102f, 0.0002f, -0.0115f, 0.9938f), new Quaternion(-0.1236f, 0.0013f, -0.0084f, 0.9923f),
            new Quaternion(-0.0188f, 0.0611f, -0.0348f, 0.9974f), new Quaternion(-0.0020f, 0.0419f, -0.0762f, 0.9962f), new Quaternion(-0.0197f, 0.0305f, -0.0018f, 0.9993f)
        };

        /// <summary>What the game's idle hand adds to a finger joint's rest pose; nothing for any other bone (jaw closed, eyes forward).</summary>
        private static Quaternion FingerCurl(string bone)
        {
            for (int finger = 0; finger < FingerNames.Length; finger++)
            {
                if (!bone.StartsWith(FingerNames[finger], StringComparison.Ordinal))
                {
                    continue;
                }
                int joint = bone.EndsWith(".001", StringComparison.Ordinal) ? 0 : bone.EndsWith(".002", StringComparison.Ordinal) ? 1 : 2;
                Quaternion idle = IdleHand[finger * 3 + joint];
                if (bone.IndexOf(".R.", StringComparison.Ordinal) >= 0)
                {
                    idle = new Quaternion(idle.x, -idle.y, -idle.z, idle.w);
                }
                return idle;
            }
            return Quaternion.identity;
        }

        private static Transform FindHips(SkinnedMeshRenderer head)
        {
            if (head.rootBone != null && head.rootBone.name == "Hips")
            {
                return head.rootBone;
            }
            Transform[] bones = head.bones;
            for (int i = 0; i < bones.Length; i++)
            {
                if (bones[i] != null && bones[i].name == "Hips")
                {
                    return bones[i];
                }
            }
            return head.rootBone;
        }

        // Hip to ankle in world units; bone lengths do not change with the pose.
        private static float LegLength(Transform hips)
        {
            Transform upper = FindBone(hips, "UpperLeg.L");
            Transform lower = FindBone(hips, "LowerLeg.L");
            Transform foot = FindBone(hips, "Foot.L");
            if (upper == null || lower == null || foot == null)
            {
                return 0f;
            }
            return Vector3.Distance(upper.position, lower.position) + Vector3.Distance(lower.position, foot.position);
        }

        private static Transform FindBone(Transform root, string name)
        {
            foreach (Transform bone in root.GetComponentsInChildren<Transform>(true))
            {
                if (bone.name == name)
                {
                    return bone;
                }
            }
            return null;
        }

        /// <summary>
        /// Once per binding, right after the first copy: every copied joint must sit where the
        /// player's does. If the two rigs had different rest orientations, copying rotations would
        /// twist the body, and this is where that shows.
        /// </summary>
        private void Verify()
        {
            _verified = true;
            MeasureGap();
            float allowed = MaxBoneGap * Mathf.Max(0.1f, _playerRoot.lossyScale.y);
            if (_worstGap > allowed)
            {
                Fail("the detailed skeleton does not line up with the player's: '" + _worstBone + "' is " + _worstGap.ToString("F2") + " m away", null);
            }
            else
            {
                Logger?.Info("High-detail player: pose check passed, largest joint offset " + _worstGap.ToString("F3") + " m at '" + _worstBone + "', hips scale " + _hipsScale.ToString("F3") + ".");
            }
        }

        private void MeasureGap()
        {
            _worstGap = 0f;
            _worstBone = "";
            for (int i = 0; i < _from.Length; i++)
            {
                if (_from[i] == null || _to[i] == null)
                {
                    continue;
                }
                float gap = Vector3.Distance(_from[i].position, _to[i].position);
                if (gap > _worstGap)
                {
                    _worstGap = gap;
                    _worstBone = _to[i].name;
                }
            }
        }

        // ---------------------------------------------------------------- stop

        /// <summary>Back to the game's own player, exactly as it was. Safe to call in any state.</summary>
        private void TearDown()
        {
            RestoreLow();
            if (_playerLook != null)
            {
                _playerLook.visualsUpdated -= _onPlayerLookChanged;
            }
            if (_playerAnimator != null)
            {
                _playerAnimator.cullingMode = _playerCulling;
            }
            CharacterQuality quality = CharacterQuality.Instance;
            if (quality != null && _doubleLook != null)
            {
                quality.Forget(_doubleLook);
            }
            if (_holder != null)
            {
                Destroy(_holder);
            }
            _holder = null;
            _doubleObject = null;
            _doubleRoot = null;
            _doubleLook = null;
            _player = null;
            _playerObject = null;
            _playerRoot = null;
            _playerLook = null;
            _playerHead = null;
            _playerAnimator = null;
            _from = Array.Empty<Transform>();
            _to = Array.Empty<Transform>();
            _fingerFrom = Array.Empty<Transform>();
            _fingerTo = Array.Empty<Transform>();
            _fingerOffset = Array.Empty<Quaternion>();
            _fromHips = _toHips = _fromArmature = _toArmature = null;
            _suspended = false;
            _shown = false;
            _lookChanged = false;
            _state = State.Waiting;
            _nextTry = Time.unscaledTime + RetrySeconds;
        }

        // Logs once, shows the player's own body again and stays off until the next load.
        private void Fail(string why, Exception error)
        {
            if (!_failed)
            {
                _failed = true;
                Logger?.Warn("High-detail player: switched off for this session, " + why + ". The game's own player is shown.");
                if (error != null)
                {
                    Logger?.Error(error);
                }
            }
            try
            {
                TearDown();
            }
            catch (Exception e)
            {
                Logger?.Error(e);
                // Whatever went wrong, make sure the body is back.
                try
                {
                    RestoreLow();
                }
                catch (Exception)
                {
                    // Nothing more can be done.
                }
            }
            enabled = false;
        }

        // ---------------------------------------------------------------- for the F9 report

        public Transform PlayerHips => _fromHips;
        public Transform DoubleHips => _toHips;
        public AppearanceSetter DoubleLook => _doubleLook;

        public string Describe()
        {
            if (_failed)
            {
                return "FAILED this session (see the log), the game's own player is shown";
            }
            if (!Settings.CharacterHighDetail)
            {
                return "switched off in the options";
            }
            if (_state != State.Active)
            {
                return "waiting for the player";
            }
            MeasureGap();
            return "ACTIVE, copied from '" + _sourceName + "', matched bones " + _from.Length + ", hips scale " + _hipsScale.ToString("F3")
                   + ", largest joint offset now " + _worstGap.ToString("F3") + " m at '" + _worstBone + "', shown " + _shown
                   + ", suspended " + _suspended + ", player renderers hidden " + _hiddenLow.Count;
        }
    }
}
