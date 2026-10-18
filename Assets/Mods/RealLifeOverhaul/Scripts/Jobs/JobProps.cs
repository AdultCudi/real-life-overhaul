using System.Collections.Generic;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The things jobs put on the street and in hands, one object a prop, made on first want and shelved between uses. Where the
    /// prop bundle lacks one, its <see cref="Alias"/> or a plain shape stands in until the modelled one arrives.
    /// </summary>
    public sealed class JobProps
    {
        private struct Spare
        {
            public Transform Prop;
            public bool Modelled;
        }

        private static readonly Color Cardboard = new Color(0.66f, 0.50f, 0.32f);
        private static readonly Color Tape = new Color(0.80f, 0.72f, 0.55f);
        private static readonly Color Dark = new Color(0.10f, 0.10f, 0.12f);
        private static readonly Color CabYellow = new Color(0.98f, 0.80f, 0.12f);
        private static readonly Color Felt = new Color(0.45f, 0.10f, 0.12f);

        private readonly Dictionary<string, Stack<Spare>> _shelf = new Dictionary<string, Stack<Spare>>(16);
        private readonly Dictionary<int, bool> _modelled = new Dictionary<int, bool>(32);   // by instance id, for what is out
        private readonly Transform _root;

        public JobProps(Transform parent)
        {
            _root = new GameObject("RealLifeOverhaul Job Props").transform;
            _root.SetParent(parent, false);
        }

        /// <summary>The prop already in the bundle that stands in for one that is on its way, or null.</summary>
        private static string Alias(string id)
        {
            switch (id)
            {
                case "flyer": return "leaflet";
                case "flyer_stack": return "leaflet_stack";
                case "parcel_s":
                case "parcel_m":
                case "parcel_l":
                case "shelf_box": return "parcel";
                case "mop_bucket": return "bucket";
                case "vendor_cart": return "coffee_cart";
                default: return null;
            }
        }

        /// <summary>True if Take would hand out something for this id right now.</summary>
        public static bool Has(string id)
        {
            return PropLibrary.TryGet(id, out _, out _) || (Alias(id) != null && PropLibrary.TryGet(Alias(id), out _, out _)) || HasShape(id);
        }

        /// <summary>A prop object, switched on, under the jobs' own root at scale one. Null if it cannot be had.</summary>
        public Transform Take(string id)
        {
            if (id == null)
            {
                return null;
            }
            if (_shelf.TryGetValue(id, out Stack<Spare> spares))
            {
                while (spares.Count > 0)
                {
                    Spare spare = spares.Pop();
                    if (spare.Prop == null)
                    {
                        continue;
                    }
                    if (!spare.Modelled && Modelled(id, out _, out _))
                    {
                        Object.Destroy(spare.Prop.gameObject);   // the stand-in has had its day
                        continue;
                    }
                    spare.Prop.gameObject.SetActive(true);
                    _modelled[spare.Prop.GetInstanceID()] = spare.Modelled;
                    return spare.Prop;
                }
            }
            var made = new GameObject(id);
            Transform prop = made.transform;
            prop.SetParent(_root, false);
            bool modelled = Modelled(id, out Mesh mesh, out Material material);
            if (modelled)
            {
                made.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = made.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            }
            else if (!Shapes.Available || !BuildShape(id, prop))
            {
                Object.Destroy(made);
                return null;
            }
            _modelled[prop.GetInstanceID()] = modelled;
            return prop;
        }

        /// <summary>Stands a prop on the ground at a point, turned about the upright axis to face a direction. Null if it cannot be had.</summary>
        public Transform Stand(string id, Vector3 position, Vector3 facing)
        {
            Transform prop = Take(id);
            if (prop != null)
            {
                facing.y = 0f;
                prop.SetPositionAndRotation(position, facing.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(facing.normalized) : Quaternion.identity);
            }
            return prop;
        }

        /// <summary>Back on the shelf, switched off. Safe with null and with a prop that went with whatever it was on.</summary>
        public void Return(Transform prop)
        {
            if (prop == null)
            {
                return;
            }
            int id = prop.GetInstanceID();
            _modelled.TryGetValue(id, out bool modelled);
            _modelled.Remove(id);
            prop.SetParent(_root, false);
            prop.localScale = Vector3.one;
            prop.gameObject.SetActive(false);
            if (!_shelf.TryGetValue(prop.name, out Stack<Spare> spares))
            {
                spares = new Stack<Spare>(4);
                _shelf[prop.name] = spares;
            }
            spares.Push(new Spare { Prop = prop, Modelled = modelled });
        }

        public void Dispose()
        {
            _shelf.Clear();
            _modelled.Clear();
            if (_root != null)
            {
                Object.Destroy(_root.gameObject);
            }
        }

        private static bool Modelled(string id, out Mesh mesh, out Material material)
        {
            if (PropLibrary.TryGet(id, out mesh, out material) && mesh != null && material != null)
            {
                return true;
            }
            string alias = Alias(id);
            return alias != null && PropLibrary.TryGet(alias, out mesh, out material) && mesh != null && material != null;
        }

        private static bool HasShape(string id)
        {
            switch (id)
            {
                case "parcel_s":
                case "parcel_m":
                case "parcel_l":
                case "shelf_box":
                case "taxi_sign":
                case "floor_patch":
                case "guitar_case":
                case "mop_bucket":
                    return Shapes.Available;
                default:
                    return false;
            }
        }

        // Each stands on its own origin at the middle of its footprint and faces +Z, as the modelled props do.
        private static bool BuildShape(string id, Transform prop)
        {
            switch (id)
            {
                case "parcel_s": Box(prop, 0.28f, 0.16f, 0.20f); return true;
                case "parcel_m": Box(prop, 0.39f, 0.24f, 0.28f); return true;
                case "parcel_l": Box(prop, 0.50f, 0.34f, 0.36f); return true;
                case "shelf_box": Box(prop, 0.42f, 0.30f, 0.32f); return true;
                case "taxi_sign":
                    Shapes.Piece("Base", prop, Shapes.Cube, Shapes.Paint(Dark), new Vector3(0f, 0.015f, 0f), new Vector3(0.46f, 0.03f, 0.18f));
                    Shapes.Piece("Lamp", prop, Shapes.Cube, Shapes.Paint(CabYellow, 1.2f), new Vector3(0f, 0.09f, 0f), new Vector3(0.40f, 0.12f, 0.12f));
                    Shapes.Piece("Band", prop, Shapes.Cube, Shapes.Paint(Dark), new Vector3(0f, 0.09f, 0f), new Vector3(0.22f, 0.045f, 0.124f));
                    return true;

                case "floor_patch":
                    // A spill: a flat disc a few millimetres proud of the floor, pale and bluish so it shows on any floor.
                    Shapes.Piece("Spill", prop, Shapes.Drum, Shapes.Paint(new Color(0.55f, 0.66f, 0.74f)), new Vector3(0f, 0.002f, 0f), new Vector3(1.05f, 0.006f, 0.85f));
                    Shapes.Piece("Splash", prop, Shapes.Drum, Shapes.Paint(new Color(0.62f, 0.72f, 0.80f)), new Vector3(0.40f, 0.002f, 0.26f), new Vector3(0.38f, 0.006f, 0.30f));
                    return true;
                case "guitar_case":
                    Shapes.Piece("Shell", prop, Shapes.Cube, Shapes.Paint(Dark), new Vector3(0f, 0.045f, 0f), new Vector3(0.40f, 0.09f, 1.06f));
                    Shapes.Piece("Lining", prop, Shapes.Cube, Shapes.Paint(Felt), new Vector3(0f, 0.088f, 0f), new Vector3(0.35f, 0.01f, 1.00f));
                    return true;
                case "mop_bucket":
                    Shapes.Piece("Pail", prop, Shapes.Drum, Shapes.Paint(new Color(0.85f, 0.75f, 0.12f)), Vector3.zero, new Vector3(0.31f, 0.28f, 0.31f));
                    return true;
                default:
                    return false;
            }
        }

        private static void Box(Transform prop, float width, float height, float length)
        {
            Shapes.Piece("Box", prop, Shapes.Cube, Shapes.Paint(Cardboard), new Vector3(0f, height * 0.5f, 0f), new Vector3(width, height, length));
            Shapes.Piece("Tape", prop, Shapes.Cube, Shapes.Paint(Tape), new Vector3(0f, height + 0.0005f, 0f), new Vector3(width * 0.16f, 0.001f, length));
        }
    }

    /// <summary>One prop in one hand at the grip measured for its clip (<see cref="ClipLibrary"/>): the first of the clips the bundle has with a tool.</summary>
    public sealed class HeldProp
    {
        private readonly HeldTool _hold = new HeldTool();
        private Transform _prop;
        private JobProps _props;

        public bool Held => _prop != null;

        /// <summary>The first of these clips that the bundle has and that carries a tool, or null.</summary>
        public static CityClip GripOf(string[] clips)
        {
            if (!ClipLibrary.Ready || clips == null)
            {
                return null;
            }
            for (int i = 0; i < clips.Length; i++)
            {
                if (ClipLibrary.TryGet(clips[i], out CityClip clip) && clip.HasTool)
                {
                    return clip;
                }
            }
            return null;
        }

        /// <summary>Puts the prop of the first usable clip in that person's hand. False, with nothing changed, if it cannot be done.</summary>
        /// <param name="prop">Another prop with the same pivot to hold at that grip (a flyer for the clip's leaflet); null for the clip's own.</param>
        public bool Give(BaseHuman who, JobProps props, string[] clips, string prop = null)
        {
            Take();
            CityClip clip = GripOf(clips);
            if (clip == null || who == null || props == null || who.animator == null || !who.animator.isHuman || !who.gameObject.activeInHierarchy)
            {
                return false;
            }
            Transform held = props.Take(prop ?? clip.Prop);
            if (held == null)
            {
                return false;
            }
            ToolGrip grip = clip.Grip;
            grip.TwoHanded = false;   // carried, not worked with: the other arm is left to whatever is playing
            if (!_hold.Attach(who.animator, held, grip))
            {
                props.Return(held);
                return false;
            }
            SetLayer(held, who.gameObject.layer);
            _prop = held;
            _props = props;
            return true;
        }

        /// <summary>Shown or hidden where it is (the person went out of view, or into a car).</summary>
        public void Show(bool show)
        {
            if (_prop != null && _prop.gameObject.activeSelf != show)
            {
                _prop.gameObject.SetActive(show);
            }
        }

        /// <summary>Out of the hand and back on the shelf. Safe at any time, also once the person has gone.</summary>
        public void Take()
        {
            if (_prop == null)
            {
                _prop = null;   // destroyed with whoever held it
                _hold.Detach(null);
                return;
            }
            Transform prop = _prop;
            _prop = null;
            _hold.Detach(null);
            SetLayer(prop, 0);
            _props.Return(prop);
        }

        private static void SetLayer(Transform prop, int layer)
        {
            prop.gameObject.layer = layer;
            for (int i = 0; i < prop.childCount; i++)
            {
                prop.GetChild(i).gameObject.layer = layer;
            }
        }
    }

    /// <summary>
    /// The flyer stack when there is no clip bundle at all: plain shapes fitted to the left hand from the line of the hanging
    /// forearm (as the paramedic's bag is), not from the bone's axes. With the bundle the stack is a <see cref="HeldProp"/>.
    /// </summary>
    public sealed class HandProp
    {
        private const float ArmDown = -0.8f;     // the forearm points at least this far down before the prop is fitted
        private const float Clearance = 0.035f;  // metres between the hand's middle and the stack, away from the leg

        private GameObject _prop;
        private bool _shown;

        /// <summary>True once it hangs in the hand; until then <see cref="Show"/> tries again each tick.</summary>
        public bool Fitted => _prop != null;

        public void Show(ThirdPersonCharacter who, bool show)
        {
            if (show && _prop == null && who != null && who.visible)
            {
                Fit(who);
            }
            if (_prop != null && show != _shown)
            {
                _shown = show;
                _prop.SetActive(show);
            }
        }

        public void Remove()
        {
            if (_prop != null)
            {
                Object.Destroy(_prop);
                _prop = null;
            }
            _shown = false;
        }

        private void Fit(ThirdPersonCharacter who)
        {
            Animator animator = who.animator;
            if (animator == null || !animator.isHuman || !Shapes.Available)
            {
                return;
            }
            Transform hand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
            Transform forearm = animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
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
            if (arm.y > ArmDown)
            {
                return;   // not hanging down just now: next tick
            }
            Vector3 outward = Vector3.Cross(arm, who.transform.forward);   // away from the body on the left side
            if (outward.sqrMagnitude < 0.25f)
            {
                return;
            }
            outward.Normalize();

            _prop = new GameObject("RealLifeOverhaul Flyers");
            Transform prop = _prop.transform;
            // Its own axes: X fore and aft, Y up the arm, Z out from the leg; the origin at the grip, on the stack's top edge.
            prop.SetPositionAndRotation(hand.position + arm * 0.06f + outward * Clearance, Quaternion.LookRotation(outward, -arm));
            prop.SetParent(hand, true);
            int layer = who.gameObject.layer;
            // A5 flyers, 0.15 by 0.21 m, a stack 2 cm thick with a printed top sheet.
            Shapes.Piece("Stack", prop, Shapes.Cube, Shapes.Paint(new Color(0.93f, 0.92f, 0.88f)), new Vector3(0f, -0.105f, 0f), new Vector3(0.15f, 0.21f, 0.02f)).gameObject.layer = layer;
            Shapes.Piece("Sheet", prop, Shapes.Cube, Shapes.Paint(new Color(0.96f, 0.76f, 0.10f)), new Vector3(0f, -0.105f, 0.0105f), new Vector3(0.15f, 0.21f, 0.001f)).gameObject.layer = layer;
            Shapes.Piece("Headline", prop, Shapes.Cube, Shapes.Paint(new Color(0.80f, 0.12f, 0.10f)), new Vector3(0f, -0.05f, 0.0112f), new Vector3(0.12f, 0.035f, 0.0006f)).gameObject.layer = layer;
            Shapes.Piece("Text", prop, Shapes.Cube, Shapes.Paint(new Color(0.15f, 0.15f, 0.17f)), new Vector3(0f, -0.135f, 0.0112f), new Vector3(0.11f, 0.07f, 0.0006f)).gameObject.layer = layer;
            _prop.layer = layer;
            _prop.SetActive(false);
            _shown = false;
        }
    }
}
