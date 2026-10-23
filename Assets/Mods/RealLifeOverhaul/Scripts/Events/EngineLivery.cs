using System;
using System.Collections.Generic;
using GleyTrafficSystem;
using UnityEngine;

namespace RealLifeOverhaul.Events
{
    /// <summary>
    /// What makes a pooled truck a fire engine, on an object of its own under it: the modelled body over the truck it is
    /// made to, the truck's own meshes hidden, with a lit lens over each end of its light bar for <see cref="BarFlash"/>;
    /// otherwise a red repaint through property blocks and a light bar on the roof. It undoes itself, every renderer as it
    /// was, when destroyed or when the truck goes back to the pool. No cost a frame: it has no Update.
    /// </summary>
    internal sealed class EngineLivery : MonoBehaviour
    {
        // Metres in the modelled body's own space (PropAssets/README.md, "Service vehicles"): X right, Y up, Z to the nose,
        // from the ground under the middle of its length.
        public const string Body = "fire_engine";
        public const string BarLens = "fire_bar_lens";
        public const float BodyLength = 7.43f;
        public const float BodyHalfWidth = 1.25f;
        public const float BodyAxleFront = 2.756f;    // the delivery truck's axles: 4.118 m apart, the rear one 2.353 m from the back
        public const float BodyAxleRear = -1.362f;
        public const float PanelOut = 1.22f;          // the pump panels, on both sides
        public const float PanelAhead = 0.756f;
        public const float OutletAhead = -0.333f;     // the discharge outlets, low on both sides
        public const float CrewDoorAhead = 1.78f;
        public const float FrontDoorAhead = 2.756f;
        public const float BarTop = 3.20f;
        private static readonly Vector3 BarAt = new Vector3(0f, 3.006f, 2.535f);   // the middle of the underside of its light bar
        private const float AxlesFit = 0.25f;         // metres of wheelbase the truck under it may differ by
        private const float LengthFit = 0.4f;         // and metres of length

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly Color EngineRed = new Color(0.78f, 0.05f, 0.04f, 1f);
        private static readonly string[] Spared = { "glass", "window", "light", "lamp", "wheel", "tire", "tyre", "rim", "chrome", "interior", "mirror", "plate", "driver" };
        private static readonly List<Renderer> RendererScratch = new List<Renderer>(32);
        private static readonly List<Material> MaterialScratch = new List<Material>(8);
        private static MaterialPropertyBlock _paint;

        private struct Painted
        {
            public Renderer Renderer;
            public int Slot;
            public MaterialPropertyBlock Before;   // null if it had none
        }

        private readonly List<Painted> _painted = new List<Painted>(24);
        private readonly List<Renderer> _hidden = new List<Renderer>(24);
        private Renderer _lensLeft, _lensRight;

        /// <summary>The modelled body, if this truck wears it; null on a repainted truck.</summary>
        public Transform Modelled { get; private set; }

        /// <summary>The lit lens over the left (0) or right (1) end of the body's light bar, switched off; null without the body.</summary>
        public Renderer Lens(int end) => end == 0 ? _lensLeft : _lensRight;

        /// <summary>Where the flashing lights of the modelled body belong: just over its light bar.</summary>
        public Vector3 Beacon => Modelled.TransformPoint(new Vector3(0f, BarTop + 0.3f, BarAt.z));

        /// <summary>True for a vehicle the body is made to: the same wheelbase and length (a van with that wheelbase is not).</summary>
        public static bool Carries(VehicleComponent car)
        {
            return Mathf.Abs(car.wheelDistance - (BodyAxleFront - BodyAxleRear)) <= AxlesFit && Mathf.Abs(car.length - BodyLength) <= LengthFit;
        }

        /// <summary>Dresses a truck: the livery, on a child object of its own, or null if nothing could be done.</summary>
        public static EngineLivery Dress(VehicleComponent car)
        {
            car.GetComponentsInChildren(false, RendererScratch);   // before anything of ours is under it
            var holder = new GameObject("RealLifeOverhaul Engine Livery");
            holder.transform.SetParent(car.transform, false);
            var livery = holder.AddComponent<EngineLivery>();
            Vector3 scale = car.transform.lossyScale;
            holder.transform.localScale = new Vector3(Inverse(scale.x), Inverse(scale.y), Inverse(scale.z));
            // Only over the truck it is made to, its axles on that truck's (whose origin is under the rear axle): on anything
            // else it would overhang one end and its wheels slide through every bend.
            float rear = 0f, front = 0f;
            bool modelled = Carries(car) && TrafficTools.Axles(car, RendererScratch, out rear, out front)
                            && Mathf.Abs(front - rear - (BodyAxleFront - BodyAxleRear)) <= AxlesFit && Shapes.Prop(Body, holder.transform);
            if (modelled)
            {
                Transform body = holder.transform.GetChild(holder.transform.childCount - 1);
                body.localPosition = new Vector3(0f, 0f, ((rear + front) * 0.5f - (BodyAxleRear + BodyAxleFront) * 0.5f) * scale.z);
                body.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;   // the truck's own shadow went with its meshes
                livery.Modelled = body;
                livery._lensLeft = LitLens(body, 180f);
                livery._lensRight = LitLens(body, 0f);
            }
            for (int i = 0; i < RendererScratch.Count; i++)
            {
                Renderer renderer = RendererScratch[i];
                if (!(renderer is MeshRenderer) && !(renderer is SkinnedMeshRenderer))
                {
                    continue;
                }
                if (modelled)
                {
                    if (!renderer.forceRenderingOff)
                    {
                        renderer.forceRenderingOff = true;
                        livery._hidden.Add(renderer);
                    }
                    continue;
                }
                livery.Repaint(renderer);
            }
            RendererScratch.Clear();
            if (!modelled)
            {
                // The light bar, on the highest part of the truck, over the middle of its length (which is not its origin).
                float roof = TrafficEffects.RoofHeight(car);
                TrafficTools.Footprint(car, out Vector3 centre, out _, out _, out _);
                var bar = new GameObject("Light Bar");
                bar.transform.SetParent(holder.transform, false);
                bar.transform.localPosition = new Vector3(0f, roof, car.transform.InverseTransformPoint(centre).z * scale.z);
                EventPropsBar.Build(bar.transform);
            }
            return livery;
        }

        // One end of the modelled light bar again, in the glow material, a hair proud of it; shown only while that end is lit.
        private static Renderer LitLens(Transform body, float turn)
        {
            if (!PropLibrary.TryGet(BarLens, out Mesh mesh, out Material material) || mesh == null || material == null)
            {
                return null;
            }
            Transform lens = Shapes.Piece(BarLens, body, mesh, material, BarAt, Vector3.one);
            lens.localRotation = Quaternion.Euler(0f, turn, 0f);
            var renderer = lens.GetComponent<MeshRenderer>();
            renderer.enabled = false;
            return renderer;
        }

        private static float Inverse(float scale)
        {
            return Mathf.Abs(scale) > 0.001f ? 1f / scale : 1f;
        }

        private void Repaint(Renderer renderer)
        {
            if (Skip(renderer.name))
            {
                return;
            }
            if (_paint == null)
            {
                _paint = new MaterialPropertyBlock();
            }
            renderer.GetSharedMaterials(MaterialScratch);
            for (int slot = 0; slot < MaterialScratch.Count; slot++)
            {
                Material material = MaterialScratch[slot];
                if (material == null || Skip(material.name))
                {
                    continue;
                }
                int property = material.HasProperty(BaseColorId) ? BaseColorId : material.HasProperty(ColorId) ? ColorId : 0;
                if (property == 0)
                {
                    continue;
                }
                MaterialPropertyBlock before = null;
                if (renderer.HasPropertyBlock())
                {
                    before = new MaterialPropertyBlock();
                    renderer.GetPropertyBlock(before, slot);
                    if (before.isEmpty)
                    {
                        before = null;
                    }
                }
                // Whatever the block already carried for this slot is kept; only the colour is ours.
                renderer.GetPropertyBlock(_paint, slot);
                _paint.SetColor(property, EngineRed);
                renderer.SetPropertyBlock(_paint, slot);
                _painted.Add(new Painted { Renderer = renderer, Slot = slot, Before = before });
            }
            MaterialScratch.Clear();
        }

        private static bool Skip(string name)
        {
            for (int i = 0; i < Spared.Length; i++)
            {
                if (name.IndexOf(Spared[i], StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }
            return false;
        }

        // Runs when the livery is destroyed or the truck under it is switched off (returned to the traffic pool).
        private void OnDisable()
        {
            for (int i = 0; i < _painted.Count; i++)
            {
                Painted painted = _painted[i];
                if (painted.Renderer != null)
                {
                    painted.Renderer.SetPropertyBlock(painted.Before, painted.Slot);
                }
            }
            _painted.Clear();
            for (int i = 0; i < _hidden.Count; i++)
            {
                if (_hidden[i] != null)
                {
                    _hidden[i].forceRenderingOff = false;
                }
            }
            _hidden.Clear();
            Destroy(gameObject);
        }
    }
}
