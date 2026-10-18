using System.Collections.Generic;
using Helpers;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The equipment that makes an officer or a paramedic unmistakable (badge, belt, radio, lettering, cap, hi-vis bands,
    /// medic bag): props hung on bones, fitted once to the measured body when the pool dresses the character. No component
    /// and nothing per frame.
    /// </summary>
    internal sealed partial class ServiceKit
    {
        public const string CapId = "police_cap";

        // The sizes the modelled belt and cap are made to (PropAssets/scripts/props.py): half width and half depth of the inside.
        private const float BeltHalfWidth = 0.150f;
        private const float BeltHalfDepth = 0.118f;
        private const float CapHalfWidth = 0.076f;
        private const float CapHalfDepth = 0.097f;
        private const float CapInside = 0.094f;     // ...and from the band's lower edge up to the inside of the top
        private const float CapBrimDrop = 0.022f;   // metres the tip of the peak hangs below the band's lower edge
        private const float CapMost = 1.22f;        // a head (hair and all) this much bigger than the cap's band keeps the game's hat instead
        private const float CapEase = 0.005f;       // metres of room between the head and the band
        private const float SkullHalfWidth = 0.074f; // metres: half an adult head's width at the upper forehead, for the proportions below
        private const float EyeDrop = 0.115f;       // on such a head: metres from the crown down to the eyes
        private const float BrowRise = 0.022f;      // ...from the eyes up to the brow
        private const float BandOverBrow = 0.020f;  // ...and from the brow up to the lower edge of a cap's band
        private const float BrimOverEyes = 0.012f;  // the least the tip of the peak may stand above the eyes
        private const float Clear = 0.004f;         // metres a piece stands off the cloth
        private const float BagGrip = 0.46f;        // metres from the wrist to the foot of the bag hanging from that hand
        private const float BandHeight = 0.05f;     // metres a reflective band of the hi-vis vest is high
        private const float BandClear = 0.012f;     // ...and stands off the body, so that it wraps the shirt instead of cutting through it

        private static readonly List<Vector3> Body = new List<Vector3>(4096);
        private static readonly List<Vector3> Head = new List<Vector3>(4096);
        private static bool _unmeasuredLogged;
        private static bool _capLogged;

        // The head's own frame from the last measurement, in the head joint's space from the pose the look was bound in, so it
        // is the same however the head is held now.
        private static bool _headFramed;
        private static Vector3 _headAcross;
        private static Vector3 _headUp;
        private static Vector3 _headForward;
        private static float _headMetres;   // metres to one unit of the head joint's space (the character's scale)

        private readonly List<GameObject> _pieces = new List<GameObject>(6);
        private readonly List<GameObject> _bands = new List<GameObject>(2);   // the hi-vis vest's reflective bands; shown only while it is worn
        private bool _vest;
        private GameObject _bag;
        private Transform _hand;
        private Vector3 _bagPosition;       // in the hand
        private Quaternion _bagRotation;
        private Vector3 _bagScale;
        private bool _visible = true;
        private bool _carried;
        private bool _down;
        private bool _stowed;               // riding on something (the stretcher), in nobody's hand
        private bool _inClip;               // a clip being played has a medic bag of its own in the hand: this one is not drawn

        /// <summary>True if the pieces are the modelled props; if not, they are made again once those load.</summary>
        public bool Modelled { get; private set; }

        /// <summary>True if the modelled cap was due but too much hair is in the way: dressed again in the game's hat.</summary>
        public bool CapRefused { get; private set; }

        /// <summary>Metres from the head joint to the top of the head, hair and all, at the last fitting; 0 if not measured.</summary>
        public float Crown { get; private set; }

        /// <summary>True if the modelled cap can be had, so an officer is dressed bare-headed and given it.</summary>
        public static bool CapToBeHad => PropLibrary.TryGet(CapId, out Mesh mesh, out Material material) && mesh != null && material != null;

        /// <summary>
        /// Fits a look's equipment to a character just dressed and switched on, removing what it wore; a civilian gets none.
        /// <paramref name="cap"/>: the officer was dressed without the game's hat, to wear the modelled cap.
        /// </summary>
        public void Fit(ThirdPersonCharacter character, ActorLook look, bool cap)
        {
            Remove();
            Modelled = PropLibrary.Ready;
            CapRefused = false;
            Crown = 0f;
            _visible = character.visible;
            Animator animator = character.animator;
            if (!Uniforms.Has(look) || !Shapes.Available || animator == null || !animator.isHuman || character.appearanceSetter == null)
            {
                return;
            }
            Transform hips = animator.GetBoneTransform(HumanBodyBones.Hips);
            Transform neck = animator.GetBoneTransform(HumanBodyBones.Neck) ?? animator.GetBoneTransform(HumanBodyBones.Head);
            Transform chest = animator.GetBoneTransform(HumanBodyBones.UpperChest) ?? animator.GetBoneTransform(HumanBodyBones.Chest)
                              ?? animator.GetBoneTransform(HumanBodyBones.Spine);
            Transform head = animator.GetBoneTransform(HumanBodyBones.Head);
            Transform armLeft = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            Transform armRight = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
            if (hips == null || neck == null || chest == null || armLeft == null || armRight == null)
            {
                return;
            }

            // The body's own frame from its joints, up the spine and across the shoulders, whatever its pose.
            Vector3 origin = hips.position;
            Vector3 up = neck.position - origin;
            float torso = up.magnitude;
            Vector3 across = armRight.position - armLeft.position;
            if (torso < 0.2f || across.sqrMagnitude < 0.01f)
            {
                return;
            }
            up /= torso;
            across -= up * Vector3.Dot(across, up);
            float shoulder = across.magnitude * 0.5f;
            Vector3 right = across.normalized;
            Vector3 forward = Vector3.Cross(right, up);
            Quaternion facing = Quaternion.LookRotation(forward, up);
            int layer = LayerHelper.HumanLayerIndex;

            bool measured = Measure(character.appearanceSetter.lastMergedMeshRenderer, animator, head, origin, right, up, forward);
            if (!measured && !_unmeasuredLogged)
            {
                _unmeasuredLogged = true;
                ActorPool.Logger?.Info("People: a uniformed character's body could not be measured; its equipment is fitted to the standard build. (Logged once.)");
            }
            Crown = HeadTop();

            // Where each piece goes: where the props were modelled (from the upper-chest joint, PropAssets/README), or by
            // proportion on a rig without it; the depth is this body's own surface when measured.
            bool upper = animator.GetBoneTransform(HumanBodyBones.UpperChest) != null;
            float jointUp = Vector3.Dot(chest.position - origin, up);
            float jointOut = Vector3.Dot(chest.position - origin, forward);
            float chestHeight = upper ? jointUp + 0.143f : torso * 0.70f;
            float chestSide = upper ? 0.07f : shoulder * 0.45f;
            float chestOut = upper ? jointOut + 0.116f : 0.105f;
            float radioHeight = upper ? jointUp + 0.193f : torso * 0.90f;
            float radioSide = upper ? 0.09f : shoulder * 0.60f;
            float radioOut = upper ? jointOut + 0.081f : 0.075f;
            float backHeight = upper ? jointUp + 0.113f : torso * 0.74f;
            float backDepth = measured && (Surface(Body, 0f, backHeight, 0.07f, -1f, out float behind) || Surface(Body, 0f, backHeight, 0.12f, -1f, out behind))
                ? behind
                : upper ? 0.111f - jointOut : 0.105f;

            if (look == ActorLook.Officer)
            {
                float beltHeight = 0.082f;
                float half = BeltHalfWidth, front = BeltHalfDepth, back = BeltHalfDepth;
                if (measured && (Section(Body, beltHeight, 0.035f, 0.24f, out float w, out float f, out float b) || Section(Body, beltHeight, 0.07f, 0.24f, out w, out f, out b)))
                {
                    half = Mathf.Clamp(w, 0.12f, 0.23f);
                    front = Mathf.Clamp(f, 0.08f, 0.26f);
                    back = Mathf.Clamp(b, 0.08f, 0.20f);
                }
                float depth = (front + back) * 0.5f;
                Transform belt = Root("Belt", hips, origin + up * beltHeight + forward * ((front - back) * 0.5f), facing, layer);
                if (Shapes.Prop("duty_belt", belt))
                {
                    belt.GetChild(0).localScale = new Vector3((half + Clear) / BeltHalfWidth, 1f, (depth + Clear) / BeltHalfDepth);
                }
                else
                {
                    Material leather = Shapes.Paint(new Color(0.03f, 0.03f, 0.035f));
                    Shapes.Piece("Strap", belt, Shapes.Drum, leather, new Vector3(0f, -0.0275f, 0f), new Vector3((half + 0.008f) * 2f, 0.055f, (depth + 0.008f) * 2f));
                    Shapes.Piece("Holster", belt, Shapes.Cube, leather, new Vector3(half + 0.022f, -0.07f, 0f), new Vector3(0.045f, 0.14f, 0.10f));
                    Shapes.Piece("Buckle", belt, Shapes.Cube, Shapes.Paint(new Color(0.70f, 0.70f, 0.68f)), new Vector3(0f, 0f, depth + 0.010f), new Vector3(0.05f, 0.04f, 0.008f));
                }

                Transform badge = Root("Badge", chest, OnChest(measured, origin, right, up, forward, -chestSide, chestHeight, chestOut), facing, layer);
                if (!Shapes.Prop("badge", badge))
                {
                    Shapes.Piece("Shield", badge, Shapes.Cube, Shapes.Paint(new Color(0.85f, 0.65f, 0.13f), 0.5f), new Vector3(0f, 0f, 0.003f), new Vector3(0.05f, 0.062f, 0.006f));
                }
                Transform radio = Root("Radio", chest, OnChest(measured, origin, right, up, forward, -radioSide, radioHeight, radioOut), facing, layer);
                if (!Shapes.Prop("shoulder_radio", radio))
                {
                    Shapes.Piece("Set", radio, Shapes.Cube, Shapes.Paint(new Color(0.10f, 0.10f, 0.11f)), new Vector3(0f, 0f, 0.014f), new Vector3(0.05f, 0.085f, 0.028f));
                }
                Panel("chest_panel_police", "POLICE", new Color(0.05f, 0.075f, 0.18f), 0.11f, 0.04f, false, chest,
                    OnChest(measured, origin, right, up, forward, chestSide, chestHeight, chestOut), facing, layer);
                Panel("back_panel_police", "POLICE", new Color(0.05f, 0.075f, 0.18f), 0.26f, 0.10f, true, chest,
                    origin + up * backHeight - forward * (backDepth + Clear), facing, layer);

                CapRefused = cap && (head == null || !FitCap(animator, head, layer));
                FitBands(animator.GetBoneTransform(HumanBodyBones.Spine) ?? hips, chest, measured, origin, up, forward, facing, torso, layer);
            }
            else
            {
                Color green = new Color(0.03f, 0.21f, 0.13f);
                Panel("chest_panel_ems", "PARAMEDIC", green, 0.09f, 0.042f, false, chest,
                    OnChest(measured, origin, right, up, forward, -chestSide, chestHeight, chestOut), facing, layer);
                Panel("back_panel_ems", "PARAMEDIC", green, 0.26f, 0.10f, true, chest,
                    origin + up * backHeight - forward * (backDepth + Clear), facing, layer);
                FitBag(animator, forward, layer);
            }

            for (int i = 0; i < _pieces.Count; i++)
            {
                SetLayer(_pieces[i].transform, layer);
            }
            Body.Clear();
            Head.Clear();
            Show(_visible);
        }

        /// <summary>Hidden and shown with the body.</summary>
        public void Show(bool visible)
        {
            _visible = visible;
            for (int i = 0; i < _pieces.Count; i++)
            {
                if (_pieces[i] != null)
                {
                    _pieces[i].SetActive(visible);
                }
            }
            if (_bag != null)
            {
                _bag.SetActive(visible && _carried && !_inClip);
            }
            for (int i = 0; i < _bands.Count; i++)
            {
                if (_bands[i] != null)
                {
                    _bands[i].SetActive(visible && _vest);
                }
            }
        }

        public void Vest(bool on)
        {
            if (_vest == on)
            {
                return;
            }
            _vest = on;
            for (int i = 0; i < _bands.Count; i++)
            {
                if (_bands[i] != null)
                {
                    _bands[i].SetActive(_visible && on);
                }
            }
        }

        public void Carry(bool carried)
        {
            if (_bag == null)
            {
                return;
            }
            if (!carried)
            {
                Stow(null, default, default);
                PutDown(null, false);
            }
            else if (_stowed)
            {
                return;   // carried, and riding on the stretcher until taken off
            }
            _carried = carried;
            _bag.SetActive(_visible && carried && !_inClip);
        }

        /// <summary>Kneeling, the bag stands on the ground at their right hand; up again, back in the hand.</summary>
        public void PutDown(Transform body, bool down)
        {
            if (_bag == null || _down == down || (down && (!_carried || _stowed || body == null)) || _hand == null)
            {
                return;
            }
            _down = down;
            Transform bag = _bag.transform;
            if (down)
            {
                bag.SetParent(body, true);
                bag.SetPositionAndRotation(body.position + body.right * 0.45f + body.forward * 0.10f, body.rotation);
            }
            else
            {
                InHand(bag);
            }
        }

        /// <summary>
        /// The bag rides on something (the stretcher, at a place in its frame), as whoever wheels it has both hands on the
        /// rail; with nothing to ride on it is back in the hand. Only a carried bag is stowed.
        /// </summary>
        public void Stow(Transform onto, Vector3 position, Quaternion rotation)
        {
            bool stow = onto != null;
            if (_bag == null || _hand == null || _stowed == stow || (stow && !_carried))
            {
                return;
            }
            Transform bag = _bag.transform;
            if (stow)
            {
                PutDown(null, false);   // never both
                _stowed = true;
                bag.SetParent(onto, true);
                bag.SetPositionAndRotation(onto.TransformPoint(position), onto.rotation * rotation);
                return;
            }
            _stowed = false;
            InHand(bag);
        }

        /// <summary>A clip holds a medic bag of its own meanwhile: this one is not drawn (never two bags).</summary>
        public void InClip(bool held)
        {
            if (_bag == null || _inClip == held)
            {
                return;
            }
            _inClip = held;
            _bag.SetActive(_visible && _carried && !held);
        }

        private void InHand(Transform bag)
        {
            bag.SetParent(_hand, false);
            bag.localPosition = _bagPosition;
            bag.localRotation = _bagRotation;
            bag.localScale = _bagScale;
        }

        /// <summary>Everything off. Safe at any time, and when the character itself has been destroyed.</summary>
        public void Remove()
        {
            for (int i = 0; i < _pieces.Count; i++)
            {
                if (_pieces[i] != null)
                {
                    Object.Destroy(_pieces[i]);
                }
            }
            _pieces.Clear();
            for (int i = 0; i < _bands.Count; i++)
            {
                if (_bands[i] != null)
                {
                    Object.Destroy(_bands[i]);
                }
            }
            _bands.Clear();
            _vest = false;
            if (_bag != null)
            {
                Object.Destroy(_bag);
            }
            _bag = null;
            _hand = null;
            _carried = false;
            _down = false;
            _stowed = false;
            _inClip = false;
        }

        private Transform Root(string name, Transform bone, Vector3 position, Quaternion rotation, int layer)
        {
            var piece = new GameObject("RealLifeOverhaul " + name);
            if (layer >= 0)
            {
                piece.layer = layer;
            }
            Transform place = piece.transform;
            place.SetPositionAndRotation(position, rotation);
            place.SetParent(bone, true);
            _pieces.Add(piece);
            return place;
        }

        private void Panel(string id, string word, Color cloth, float width, float height, bool onBack, Transform bone, Vector3 position, Quaternion facing, int layer)
        {
            Transform panel = Root(id, bone, position, facing, layer);
            if (Shapes.Prop(id, panel))
            {
                return;
            }
            Shapes.Piece("Plate", panel, Shapes.Cube, Shapes.Paint(cloth), Vector3.zero, new Vector3(width, height, 0.004f));
            TMP_FontAsset font = GameUi.GameFont.Asset;
            if (font == null)
            {
                return;   // no lettering to be had: the plate alone
            }
            var label = new GameObject("Lettering");
            Transform place = label.transform;
            place.SetParent(panel, false);
            // Text is read from behind its own forward: turned to face out from the body.
            place.localPosition = new Vector3(0f, 0f, onBack ? -0.004f : 0.004f);
            place.localRotation = onBack ? Quaternion.identity : Quaternion.Euler(0f, 180f, 0f);
            var text = label.AddComponent<TextMeshPro>();
            text.font = font;
            text.text = word;
            text.color = Color.white;
            text.fontStyle = FontStyles.Bold;
            text.alignment = TextAlignmentOptions.Center;
#pragma warning disable CS0618
            text.enableWordWrapping = false;
#pragma warning restore CS0618
            text.rectTransform.sizeDelta = new Vector2(width * 0.92f, height * 0.80f);
            text.enableAutoSizing = true;
            text.fontSizeMin = 0.02f;
            text.fontSizeMax = 3f;
            var renderer = label.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
            SetLayer(panel, layer);
        }

        // Two silver bands round the trunk, above the belt and under the chest, each sized to the body where it sits; off
        // until the vest is worn.
        private void FitBands(Transform spine, Transform chest, bool measured, Vector3 origin, Vector3 up, Vector3 forward, Quaternion facing, float torso, int layer)
        {
            Material silver = Shapes.Paint(new Color(0.80f, 0.81f, 0.78f), 1.5f);
            if (silver == null)
            {
                return;
            }
            for (int i = 0; i < 2; i++)
            {
                float height = torso * (i == 0 ? 0.30f : 0.56f);
                float half = 0.165f, front = 0.115f, back = 0.105f;
                if (measured && (Section(Body, height, 0.03f, 0.26f, out float w, out float f, out float b) || Section(Body, height, 0.06f, 0.26f, out w, out f, out b)))
                {
                    half = Mathf.Clamp(w, 0.12f, 0.25f);
                    front = Mathf.Clamp(f, 0.08f, 0.28f);
                    back = Mathf.Clamp(b, 0.08f, 0.22f);
                }
                float depth = (front + back) * 0.5f;
                var band = new GameObject("RealLifeOverhaul Vest Band");
                Transform place = band.transform;
                place.SetPositionAndRotation(origin + up * height + forward * ((front - back) * 0.5f), facing);
                place.SetParent(i == 0 ? spine : chest, true);
                Shapes.Piece("Ring", place, Shapes.Drum, silver, new Vector3(0f, -BandHeight * 0.5f, 0f),
                    new Vector3((half + BandClear) * 2f, BandHeight, (depth + BandClear) * 2f));
                SetLayer(place, layer);
                band.SetActive(false);
                _bands.Add(band);
            }
        }

        // The cap is placed in the head's own frame as measured, so it sits level however the head is held. The band's
        // lower edge goes a finger above the brow (eyes from the eye joints, else by proportion), raised until the peak
        // clears the eyes and the crown fits inside. A head much bigger than the band, or unmeasured, is refused (false).
        private bool FitCap(Animator animator, Transform head, int layer)
        {
            float top = Crown;
            if (!_headFramed || top <= 0f)
            {
                return false;
            }
            float size = Section(Head, top - 0.065f, 0.02f, 0.2f, out float skull, out _, out _) ? Mathf.Clamp(skull / SkullHalfWidth, 0.85f, 1.2f) : 1f;
            float eyes = top - EyeDrop * size;
            bool jointed = false;
            Transform eyeLeft = animator.GetBoneTransform(HumanBodyBones.LeftEye);
            Transform eyeRight = animator.GetBoneTransform(HumanBodyBones.RightEye);
            if (eyeLeft != null && eyeRight != null)
            {
                float found = Vector3.Dot(head.InverseTransformPoint((eyeLeft.position + eyeRight.position) * 0.5f), _headUp) * _headMetres;
                if (found < top - 0.07f * size && found > top - 0.17f * size)   // eye joints anywhere else are not where the eyes are drawn
                {
                    eyes = found;
                    jointed = true;
                }
            }
            float band = eyes + (BrowRise + BandOverBrow) * size;

            if (!Section(Head, band + 0.02f * size, 0.025f * size, 0.2f, out float w, out float f, out float b)
                && !Section(Head, band + 0.02f * size, 0.05f * size, 0.2f, out w, out f, out b))
            {
                return false;
            }
            float depth = (f + b) * 0.5f;
            if (w > CapHalfWidth * CapMost || depth > CapHalfDepth * CapMost)
            {
                return false;
            }
            float wide = (Mathf.Max(w, CapHalfWidth * 0.88f) + CapEase) / CapHalfWidth;
            float deep = (Mathf.Max(depth, CapHalfDepth * 0.88f) + CapEase) / CapHalfDepth;
            float tall = (wide + deep) * 0.5f;
            band = Mathf.Max(band, eyes + BrimOverEyes + CapBrimDrop * tall);   // the tip of the peak ends above the eyes
            band = Mathf.Max(band, top - CapInside * tall);                     // the crown is inside the cap

            Vector3 place = head.TransformPoint((_headUp * band + _headForward * ((f - b) * 0.5f)) / _headMetres);
            Transform cap = Root("Cap", head, place, head.rotation * Quaternion.LookRotation(_headForward, _headUp), layer);
            if (Shapes.Prop(CapId, cap))
            {
                cap.GetChild(0).localScale = new Vector3(wide, tall, deep);
            }
            if (!_capLogged)
            {
                _capLogged = true;
                ActorPool.Logger?.Info(System.FormattableString.Invariant($"People: police cap fitted to the measured head: crown {top:F3} m above the head joint, eyes at {eyes:F3} m ")
                                       + System.FormattableString.Invariant($"({(jointed ? "from the eye joints" : "by proportion, no usable eye joints")}), head {size:F2} of the standard width; ")
                                       + System.FormattableString.Invariant(
                                           $"band's lower edge at {band:F3} m ({band - eyes:F3} m above the eyes), tip of the peak {band - CapBrimDrop * tall - eyes:F3} m above the eyes; ")
                                       + System.FormattableString.Invariant(
                                           $"skull there {w * 2f:F3} m wide and {depth * 2f:F3} m deep, cap scaled {wide:F2} x {tall:F2} x {deep:F2}. (Logged once.)"));
            }
            return true;
        }

        // The highest point of the measured head above its joint, in metres; 0 if the head was not measured.
        private static float HeadTop()
        {
            if (!_headFramed || Head.Count < 40)
            {
                return 0f;
            }
            float top = 0f;
            for (int i = 0; i < Head.Count; i++)
            {
                top = Mathf.Max(top, Head[i].y);
            }
            return top > 0.08f && top < 0.45f ? top : 0f;   // anything else is not a head on its joint
        }

        // The bag hangs from the right hand along the forearm, so it is below the hand whenever the arm is down.
        private void FitBag(Animator animator, Vector3 forward, int layer)
        {
            Transform hand = animator.GetBoneTransform(HumanBodyBones.RightHand);
            Transform forearm = animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
            if (hand == null || forearm == null)
            {
                return;
            }
            Vector3 arm = hand.position - forearm.position;
            if (arm.sqrMagnitude < 0.0001f)
            {
                return;
            }
            arm.Normalize();
            Vector3 outward = Vector3.Cross(forward, arm);
            if (outward.sqrMagnitude < 0.01f)
            {
                return;   // the arm points straight ahead: no telling which way the bag should face
            }
            _bag = new GameObject("RealLifeOverhaul Medic Bag");
            if (layer >= 0)
            {
                _bag.layer = layer;
            }
            Transform bag = _bag.transform;
            bag.SetPositionAndRotation(hand.position + arm * BagGrip, Quaternion.LookRotation(outward.normalized, -arm));
            bag.SetParent(hand, true);
            if (!Shapes.Prop("medic_bag", bag))
            {
                Shapes.Piece("Body", bag, Shapes.Cube, Shapes.Paint(new Color(0.72f, 0.06f, 0.05f)), new Vector3(0f, 0.16f, 0f), new Vector3(0.54f, 0.32f, 0.25f));
                Shapes.Piece("Band", bag, Shapes.Cube, Shapes.Paint(new Color(0.92f, 0.92f, 0.90f), 1f), new Vector3(0f, 0.19f, 0f), new Vector3(0.55f, 0.035f, 0.255f));
                Shapes.Piece("Handle", bag, Shapes.Cube, Shapes.Paint(new Color(0.04f, 0.04f, 0.04f)), new Vector3(0f, 0.36f, 0f), new Vector3(0.20f, 0.08f, 0.03f));
            }
            SetLayer(bag, layer);
            _hand = hand;
            _bagPosition = bag.localPosition;
            _bagRotation = bag.localRotation;
            _bagScale = bag.localScale;
            _bag.SetActive(false);
        }

        private static void SetLayer(Transform root, int layer)
        {
            if (layer < 0)
            {
                return;
            }
            root.gameObject.layer = layer;
            for (int i = 0; i < root.childCount; i++)
            {
                SetLayer(root.GetChild(i), layer);
            }
        }

        private static Vector3 OnChest(bool measured, Vector3 origin, Vector3 right, Vector3 up, Vector3 forward, float across, float height, float standard)
        {
            // The patch is widened once: these are low-detail characters, and a small patch can fall between their points.
            float depth = measured && (Surface(Body, across, height, 0.04f, 1f, out float found) || Surface(Body, across, height, 0.08f, 1f, out found)) ? found : standard;
            return origin + right * across + up * height + forward * (depth + Clear);
        }
    }
}
