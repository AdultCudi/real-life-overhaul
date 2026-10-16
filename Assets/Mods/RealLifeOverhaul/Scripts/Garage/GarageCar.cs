using System;
using System.Collections.Generic;
using BAModAPI;
using NWH.Common.Vehicles;
using NWH.VehiclePhysics2.Powertrain;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;
using NwhVehicle = NWH.VehiclePhysics2.VehicleController;

namespace RealLifeOverhaul
{
    /// <summary>
    /// One of the player's cars wearing its build: paint and finish, lamp tint, underglow, the fitted
    /// parts, and the parts that change how it stops, grips and rides. Sits on the car's own object, so it
    /// goes when the car does. Everything is done once when the build changes (never per frame), through
    /// the renderers' property blocks the game itself paints cars with, and everything it touches is
    /// recorded first: <see cref="Clear"/> leaves the car exactly as the game made it.
    ///
    /// How the game paints a car: every car is one body mesh on one material of the shader
    /// "Shader Graphs/SH_Vehicle" (paint, glass, lamps and trim share one texture; a mask in it says where
    /// the paint is). <c>CarFeatures.SetColor</c> writes three properties into a MaterialPropertyBlock on
    /// each of <c>CarFeatures.bodyMeshes</c>: Tint, FresnelColor (the colour the paint turns at a glancing
    /// angle) and FresnelPower. The shader has no metallic or clear-coat property; what it does have is
    /// SmoothnessBoost, which with the two fresnel values is what a finish is made of here.
    /// </summary>
    public sealed class GarageCar : MonoBehaviour
    {
        // The vehicle shader's own property names (a shader graph keeps the generated ones).
        private static readonly int TintId = Shader.PropertyToID("Color_3d0f0cdbe6b74be28a1a5be5bab71dea");
        private static readonly int FresnelColourId = Shader.PropertyToID("Color_f78fac473bac467092fb27521e9f71ea");
        private static readonly int FresnelPowerId = Shader.PropertyToID("Vector1_481fa2a8a5e94165a039319bfd512b76");
        private static readonly int GlossId = Shader.PropertyToID("Vector1_41991b689ccf4932ad504975ac34399a");
        private static readonly int LampColourId = Shader.PropertyToID("_EmissionColor");

        private const float WheelPropRadius = 0.34f;   // the size the wheel props are modelled at
        private const float WheelPropWidth = 0.22f;
        private const float GlowLumens = 900f;

        public static IModLogger Logger;

        private static MaterialPropertyBlock _block;
        private static readonly List<Light> LightScratch = new List<Light>(4);

        private struct WheelStock
        {
            public WheelUAPI Wheel;
            public float Grip, SideGrip, SpringForce, SpringLength, Bump, Rebound;
        }

        private struct WheelLook
        {
            public Renderer Hidden;
            public GameObject Fitted;
        }

        private VehicleController _vehicle;
        private CarFeatures _features;
        private readonly GarageBuild _shown = new GarageBuild();
        private bool _any;

        // looks
        private bool _painted;
        private bool _lampsTinted;
        private Light[] _lamps;
        private Color[] _lampColours;
        private Light _glow;
        private HDAdditionalLightData _glowData;
        private GameObject _parts;
        private bool _partSpoiler, _partRack, _partBar;
        private string _wheelsShown = string.Empty;
        private readonly List<WheelLook> _wheelLooks = new List<WheelLook>(6);

        // handling
        private bool _handling;
        private GarageModifiers _handled;
        private bool _wet;
        private NwhVehicle _nwh;
        private float _brakeTorque;
        private float _mass, _baseMass;
        private float _pitch, _volume;
        private float[] _antiRoll;
        private readonly List<WheelStock> _wheels = new List<WheelStock>(6);

        /// <summary>What the director last showed on this car (build version and options); -1: nothing yet.</summary>
        public int Stamp = -1;

        /// <summary>The build this car is showing (a preview in the shop, or its own).</summary>
        public GarageBuild Shown => _shown;

        public VehicleController Vehicle => _vehicle;

        public static GarageCar Of(VehicleController vehicle, bool create)
        {
            if (vehicle == null)
            {
                return null;
            }
            GarageCar car = vehicle.GetComponent<GarageCar>();
            if (car == null && create)
            {
                car = vehicle.gameObject.AddComponent<GarageCar>();
                car._vehicle = vehicle;
                car._features = vehicle.GetComponent<CarFeatures>();
            }
            return car;
        }

        /// <summary>Puts a build on the car. Only what differs from what it shows now is redone.</summary>
        public void Show(GarageBuild build, bool looks, bool handling, bool wet)
        {
            if (_vehicle == null || build == null)
            {
                return;
            }
            try
            {
                if (looks)
                {
                    if (!_any || !_painted || build.Paint != _shown.Paint || build.Finish != _shown.Finish)
                    {
                        Paint(build.Paint, build.Finish);
                    }
                    if (!_any || build.Lamps != _shown.Lamps)
                    {
                        Lamps(build.Lamps);
                    }
                    if (!_any || build.Glow != _shown.Glow)
                    {
                        Glow(build.Glow);
                    }
                    if (!_any || build.Spoiler != _partSpoiler || build.Rack != _partRack || build.Bar != _partBar)
                    {
                        Fit(build.Spoiler, build.Rack, build.Bar);
                    }
                    if (!_any || build.Wheels != _wheelsShown)
                    {
                        Wheels(build.Wheels);
                    }
                }
                else
                {
                    ClearLooks();
                }
                GarageModifiers m = build.Modifiers();
                if (handling && build.HasParts)
                {
                    if (!_handling || wet != _wet || !Same(m, _handled))
                    {
                        Handling(m, wet);
                    }
                }
                else
                {
                    ClearHandling();
                }
                _shown.Take(build);
                _any = true;
            }
            catch (Exception e)
            {
                Logger?.Warn("Garage: a build could not be put on " + _vehicle.name + "; the car is back as the game made it. " + e);
                Clear();
            }
        }

        /// <summary>The car exactly as the game made it.</summary>
        public void Clear()
        {
            ClearLooks();
            ClearHandling();
            _shown.Take(new GarageBuild());
            _any = false;
        }

        private void OnDestroy()
        {
            // The car is going: its renderers, lights and physics go with it. Only what could outlive it matters,
            // and nothing here does (the pieces are its children, the meshes and materials are shared).
            _parts = null;
        }

        // ---------------------------------------------------------------- paint

        private void Paint(int colour, PaintFinish finish)
        {
            Renderer[] bodies = _features != null ? _features.bodyMeshes : null;
            if (bodies == null || bodies.Length == 0)
            {
                return;
            }
            if (colour < 0 && finish == PaintFinish.Gloss)
            {
                Unpaint(bodies);
                return;
            }
            if (_block == null)
            {
                _block = new MaterialPropertyBlock();
            }
            for (int i = 0; i < bodies.Length; i++)
            {
                Renderer body = bodies[i];
                Material material = body != null ? body.sharedMaterial : null;
                if (material == null || !material.HasProperty(TintId))
                {
                    continue;   // not the vehicle shader (a car from another mod): left alone
                }
                Color tint = colour >= 0 ? FromRgb(colour) : StockTint(material);
                float gloss = material.HasProperty(GlossId) ? material.GetFloat(GlossId) : 1f;
                Color edge;
                float power;
                switch (finish)
                {
                    case PaintFinish.Satin:
                        edge = Color.Lerp(tint, Color.white, 0.15f);
                        power = 1.5f;
                        gloss *= 0.72f;
                        break;
                    case PaintFinish.Matte:
                        edge = tint;
                        power = 1f;
                        gloss *= 0.45f;
                        break;
                    case PaintFinish.Metallic:
                        edge = Color.Lerp(tint, Color.white, 0.6f);
                        power = 2.2f;
                        gloss *= 1.15f;
                        break;
                    case PaintFinish.Pearl:
                        Color.RGBToHSV(tint, out float h, out float s, out float v);
                        edge = Color.HSVToRGB(Mathf.Repeat(h + 0.11f, 1f), Mathf.Clamp01(s * 0.8f + 0.15f), Mathf.Clamp01(v * 0.7f + 0.3f));
                        power = 0.7f;
                        gloss *= 1.1f;
                        break;
                    default:
                        edge = Color.Lerp(tint, Color.white, 0.35f);
                        power = 1f;
                        break;
                }
                body.GetPropertyBlock(_block);
                _block.SetColor(TintId, tint);
                _block.SetColor(FresnelColourId, edge);
                _block.SetFloat(FresnelPowerId, power);
                if (material.HasProperty(GlossId))
                {
                    _block.SetFloat(GlossId, gloss);
                }
                body.SetPropertyBlock(_block);
            }
            _painted = true;
        }

        // The colour the car was bought in, the way the game put it on.
        private void Unpaint(Renderer[] bodies)
        {
            if (!_painted)
            {
                return;
            }
            _painted = false;
            if (_block == null)
            {
                _block = new MaterialPropertyBlock();
            }
            Data.VehicleColors.VehicleColor stock = _features.VehicleColor;
            for (int i = 0; i < bodies.Length; i++)
            {
                Renderer body = bodies[i];
                Material material = body != null ? body.sharedMaterial : null;
                if (material == null || !material.HasProperty(TintId))
                {
                    continue;
                }
                body.GetPropertyBlock(_block);
                if (stock != null)
                {
                    _block.SetColor(TintId, stock.tint);
                    _block.SetColor(FresnelColourId, stock.fresnelColor);
                    _block.SetFloat(FresnelPowerId, stock.fresnelPower);
                }
                else
                {
                    _block.SetColor(TintId, material.GetColor(TintId));
                    _block.SetColor(FresnelColourId, material.GetColor(FresnelColourId));
                    _block.SetFloat(FresnelPowerId, material.GetFloat(FresnelPowerId));
                }
                if (material.HasProperty(GlossId))
                {
                    _block.SetFloat(GlossId, material.GetFloat(GlossId));
                }
                body.SetPropertyBlock(_block);
            }
        }

        private Color StockTint(Material material)
        {
            Data.VehicleColors.VehicleColor stock = _features.VehicleColor;
            Color tint = stock != null ? (Color)stock.tint : material.GetColor(TintId);
            tint.a = 1f;
            return tint;
        }

        /// <summary>The colour this car was bought in, for the shop's swatch. Grey if it cannot be told.</summary>
        public Color StockColour()
        {
            Renderer[] bodies = _features != null ? _features.bodyMeshes : null;
            Material material = bodies != null && bodies.Length > 0 && bodies[0] != null ? bodies[0].sharedMaterial : null;
            return material != null && material.HasProperty(TintId) ? StockTint(material) : Color.grey;
        }

        /// <summary>False for a vehicle the game's paint does not reach (no body renderers on the vehicle shader).</summary>
        public bool Paintable
        {
            get
            {
                Renderer[] bodies = _features != null ? _features.bodyMeshes : null;
                Material material = bodies != null && bodies.Length > 0 && bodies[0] != null ? bodies[0].sharedMaterial : null;
                return material != null && material.HasProperty(TintId);
            }
        }

        public static Color FromRgb(int colour)
        {
            return new Color(((colour >> 16) & 0xFF) / 255f, ((colour >> 8) & 0xFF) / 255f, (colour & 0xFF) / 255f, 1f);
        }

        public static int ToRgb(Color colour)
        {
            Color32 c = colour;
            return c.r << 16 | c.g << 8 | c.b;
        }

        // ---------------------------------------------------------------- lamps

        private void Lamps(LampTint tint)
        {
            Renderer[] bodies = _features != null ? _features.bodyMeshes : null;
            if (tint == LampTint.Stock)
            {
                Unlamp(bodies);
                return;
            }
            Color colour = tint == LampTint.Warm ? new Color(1f, 0.82f, 0.58f)
                : tint == LampTint.Ice ? new Color(0.72f, 0.86f, 1f)
                : new Color(1f, 0.85f, 0.25f);
            if (_lamps == null)
            {
                // The car's own headlamp lights, found once. Ours (the underglow) is never one of them.
                LightScratch.Clear();
                _vehicle.GetComponentsInChildren(true, LightScratch);
                if (_glow != null)
                {
                    LightScratch.Remove(_glow);
                }
                _lamps = LightScratch.ToArray();
                _lampColours = new Color[_lamps.Length];
                for (int i = 0; i < _lamps.Length; i++)
                {
                    _lampColours[i] = _lamps[i].color;
                }
                LightScratch.Clear();
            }
            for (int i = 0; i < _lamps.Length; i++)
            {
                if (_lamps[i] != null)
                {
                    _lamps[i].color = _lampColours[i] * colour;
                }
            }
            if (bodies != null)
            {
                if (_block == null)
                {
                    _block = new MaterialPropertyBlock();
                }
                for (int i = 0; i < bodies.Length; i++)
                {
                    Material material = bodies[i] != null ? bodies[i].sharedMaterial : null;
                    if (material == null || !material.HasProperty(TintId) || !material.HasProperty(LampColourId))
                    {
                        continue;
                    }
                    bodies[i].GetPropertyBlock(_block);
                    _block.SetColor(LampColourId, material.GetColor(LampColourId) * colour);
                    bodies[i].SetPropertyBlock(_block);
                }
            }
            _lampsTinted = true;
        }

        private void Unlamp(Renderer[] bodies)
        {
            if (!_lampsTinted)
            {
                return;
            }
            _lampsTinted = false;
            if (_lamps != null)
            {
                for (int i = 0; i < _lamps.Length; i++)
                {
                    if (_lamps[i] != null)
                    {
                        _lamps[i].color = _lampColours[i];
                    }
                }
            }
            if (bodies == null)
            {
                return;
            }
            if (_block == null)
            {
                _block = new MaterialPropertyBlock();
            }
            for (int i = 0; i < bodies.Length; i++)
            {
                Material material = bodies[i] != null ? bodies[i].sharedMaterial : null;
                if (material == null || !material.HasProperty(TintId) || !material.HasProperty(LampColourId))
                {
                    continue;
                }
                bodies[i].GetPropertyBlock(_block);
                _block.SetColor(LampColourId, material.GetColor(LampColourId));
                bodies[i].SetPropertyBlock(_block);
            }
        }

        // ---------------------------------------------------------------- underglow

        private void Glow(int colour)
        {
            if (colour < 0)
            {
                if (_glow != null)
                {
                    Destroy(_glow.gameObject);
                    _glow = null;
                    _glowData = null;
                }
                return;
            }
            if (_glow == null)
            {
                float reach = 3.5f;
                if (GarageFit.TryGet(TypeName, out CarFit fit))
                {
                    reach = Mathf.Max(3.5f, (fit.Front - fit.Rear) * 0.5f + 1.2f);
                }
                var go = new GameObject("RealLifeOverhaul Underglow");
                go.transform.SetParent(_vehicle.transform, false);
                go.transform.localPosition = new Vector3(0f, 0.14f, 0f);
                _glow = go.AddComponent<Light>();
                _glow.type = LightType.Point;
                _glow.range = reach;
                _glow.shadows = LightShadows.None;
                _glowData = go.AddComponent<HDAdditionalLightData>();
                _glowData.SetIntensity(GlowLumens, LightUnit.Lumen);
                _glowData.range = reach;
                go.SetActive(false);   // the director lights it after dark
            }
            _glow.color = FromRgb(colour);
        }

        /// <summary>Night, the option and the shop decide; called from the director's slow tick, and only does anything on a change.</summary>
        public void LightGlow(bool on)
        {
            if (_glow != null && _glow.gameObject.activeSelf != on)
            {
                _glow.gameObject.SetActive(on);
            }
        }

        public bool HasGlow => _glow != null;

        // ---------------------------------------------------------------- fitted parts

        private string TypeName => _vehicle.vehicleType != null ? _vehicle.vehicleType.vehicleTypeName : null;

        private void Fit(bool spoiler, bool rack, bool bar)
        {
            if (_parts != null)
            {
                Destroy(_parts);
                _parts = null;
            }
            _partSpoiler = spoiler;
            _partRack = rack;
            _partBar = bar;
            if (!(spoiler || rack || bar) || !GarageFit.TryGet(TypeName, out CarFit fit) || !Shapes.Available)
            {
                return;
            }
            CarKind kind = GarageShop.KindOf(_vehicle);
            _parts = new GameObject("RealLifeOverhaul Fitted Parts");
            Transform root = _parts.transform;
            root.SetParent(_vehicle.transform, false);
            Material black = Shapes.Paint(new Color(0.035f, 0.035f, 0.04f));

            if (spoiler && GarageFit.Spoiler(TypeName, kind))
            {
                float z = fit.Rear + 0.26f;
                float half = Mathf.Min(fit.DeckHalfWidth * 0.92f, fit.HalfWidth - 0.08f);
                float wing = Mathf.Max(fit.DeckHeight, fit.DeckPostHeight) + 0.16f;
                if (!Modelled("car_spoiler", root, new Vector3(0f, fit.DeckPostHeight, z), new Vector3(half / 0.70f, 1f, 1f)))
                {
                    float post = fit.DeckHalfWidth * 0.55f;
                    float foot = fit.DeckPostHeight - 0.015f;   // sunk a little into the deck, so no gap shows under a foot
                    Shapes.Piece("Wing", root, Shapes.Cube, black, new Vector3(0f, wing, z), new Vector3(half * 2f, 0.025f, 0.24f));
                    Shapes.Piece("Post L", root, Shapes.Cube, black, new Vector3(-post, (wing + foot) * 0.5f, z), new Vector3(0.03f, wing - foot, 0.10f));
                    Shapes.Piece("Post R", root, Shapes.Cube, black, new Vector3(post, (wing + foot) * 0.5f, z), new Vector3(0.03f, wing - foot, 0.10f));
                    Shapes.Piece("Plate L", root, Shapes.Cube, black, new Vector3(-half, wing + 0.02f, z), new Vector3(0.012f, 0.09f, 0.26f));
                    Shapes.Piece("Plate R", root, Shapes.Cube, black, new Vector3(half, wing + 0.02f, z), new Vector3(0.012f, 0.09f, 0.26f));
                }
            }

            if (rack && GarageFit.Rack(TypeName, kind))
            {
                float length = fit.RoofTo - fit.RoofFrom;
                float barHeight = fit.RoofHeight + 0.085f;
                float foot = fit.RoofFootHeight - 0.02f;
                float feet = fit.RoofHalfWidth * 0.92f;
                for (int i = 0; i < 2; i++)
                {
                    float z = i == 0 ? fit.RoofFrom + 0.22f * length : fit.RoofTo - 0.22f * length;
                    if (Modelled("car_roof_bar", root, new Vector3(0f, fit.RoofFootHeight, z), new Vector3((fit.RoofHalfWidth + 0.04f) / 0.60f, 1f, 1f)))
                    {
                        continue;
                    }
                    Shapes.Piece("Bar", root, Shapes.Cube, black, new Vector3(0f, barHeight, z), new Vector3((fit.RoofHalfWidth + 0.04f) * 2f, 0.03f, 0.05f));
                    Shapes.Piece("Foot L", root, Shapes.Cube, black, new Vector3(-feet, (barHeight + foot) * 0.5f, z), new Vector3(0.05f, barHeight - foot, 0.06f));
                    Shapes.Piece("Foot R", root, Shapes.Cube, black, new Vector3(feet, (barHeight + foot) * 0.5f, z), new Vector3(0.05f, barHeight - foot, 0.06f));
                }
            }

            if (bar && GarageFit.Bar(TypeName, kind))
            {
                float z = fit.Front + 0.07f;
                float half = Mathf.Min(fit.NoseHalfWidth, fit.HalfWidth - 0.10f) * 0.85f;
                float low = fit.NoseLow + 0.05f;
                float high = GarageFit.BarTop(fit);
                if (!Modelled("car_bull_bar", root, new Vector3(0f, low, z), new Vector3(half / 0.50f, (high - low) / 0.60f, 1f)))
                {
                    const float tube = 0.05f;
                    float upright = half * 0.55f;
                    Tube("Upright L", root, black, new Vector3(-upright, low, z), Quaternion.identity, high - low, tube);
                    Tube("Upright R", root, black, new Vector3(upright, low, z), Quaternion.identity, high - low, tube);
                    Tube("Top", root, black, new Vector3(-half, high, z), Quaternion.Euler(0f, 0f, -90f), half * 2f, tube);
                    Tube("Rail", root, black, new Vector3(-half, low + 0.14f, z), Quaternion.Euler(0f, 0f, -90f), half * 2f, tube);
                    Tube("Mount L", root, black, new Vector3(-upright, low + 0.14f, fit.Front - 0.05f), Quaternion.Euler(90f, 0f, 0f), 0.12f, tube * 0.8f);
                    Tube("Mount R", root, black, new Vector3(upright, low + 0.14f, fit.Front - 0.05f), Quaternion.Euler(90f, 0f, 0f), 0.12f, tube * 0.8f);
                }
            }
        }

        // A modelled part from the prop bundle, when it has one (see the wish-list in docs/features/garage.md).
        private static bool Modelled(string id, Transform root, Vector3 at, Vector3 scale)
        {
            if (!PropLibrary.TryGet(id, out Mesh mesh, out Material material) || mesh == null || material == null)
            {
                return false;
            }
            Shapes.Piece(id, root, mesh, material, at, scale);
            return true;
        }

        // A straight round tube from a point, along the rotation's up.
        private static void Tube(string name, Transform root, Material paint, Vector3 from, Quaternion along, float length, float thickness)
        {
            Transform tube = Shapes.Piece(name, root, Shapes.Drum, paint, from, new Vector3(thickness, length, thickness));
            tube.localRotation = along;
        }

        // ---------------------------------------------------------------- wheels

        private void Wheels(string id)
        {
            for (int i = 0; i < _wheelLooks.Count; i++)
            {
                if (_wheelLooks[i].Hidden != null)
                {
                    _wheelLooks[i].Hidden.forceRenderingOff = false;
                }
                if (_wheelLooks[i].Fitted != null)
                {
                    Destroy(_wheelLooks[i].Fitted);
                }
            }
            _wheelLooks.Clear();
            _wheelsShown = id ?? string.Empty;
            if (_wheelsShown.Length == 0 || !PropLibrary.TryGet(_wheelsShown, out Mesh mesh, out Material material) || mesh == null || material == null)
            {
                return;   // no such wheel in the bundle (yet): the car keeps its own, and nothing stands in for a wheel
            }
            NwhVehicle nwh = Nwh();
            if (nwh == null || nwh.powertrain == null || nwh.powertrain.wheels == null)
            {
                return;
            }
            foreach (WheelComponent component in nwh.powertrain.wheels)
            {
                WheelUAPI wheel = component != null ? component.wheelUAPI : null;
                GameObject visual = wheel != null ? wheel.WheelVisual : null;
                Renderer own = visual != null ? visual.GetComponent<Renderer>() : null;
                if (own == null || !(wheel.Radius > 0.1f))
                {
                    continue;
                }
                // Sized to the wheel the car has: its radius from the physics, its width from its own mesh.
                float width = own is MeshRenderer && visual.TryGetComponent(out MeshFilter filter) && filter.sharedMesh != null
                    ? filter.sharedMesh.bounds.size.x * Mathf.Abs(visual.transform.lossyScale.x)
                    : wheel.Width;
                width = Mathf.Clamp(width, 0.12f, 0.45f);
                Transform fitted = Shapes.Piece(_wheelsShown, visual.transform, mesh, material, Vector3.zero,
                    new Vector3(width / WheelPropWidth, wheel.Radius / WheelPropRadius, wheel.Radius / WheelPropRadius));
                if (_vehicle.transform.InverseTransformPoint(visual.transform.position).x > 0f)
                {
                    fitted.localRotation = Quaternion.Euler(0f, 180f, 0f);   // the outer face outwards on both sides
                }
                own.forceRenderingOff = true;
                _wheelLooks.Add(new WheelLook { Hidden = own, Fitted = fitted.gameObject });
            }
        }

        private void ClearLooks()
        {
            Renderer[] bodies = _features != null ? _features.bodyMeshes : null;
            if (bodies != null)
            {
                Unpaint(bodies);
            }
            _painted = false;
            Unlamp(bodies);
            Glow(-1);
            if (_parts != null)
            {
                Destroy(_parts);
                _parts = null;
            }
            _partSpoiler = _partRack = _partBar = false;
            if (_wheelLooks.Count > 0 || _wheelsShown.Length > 0)
            {
                Wheels(string.Empty);
            }
        }

        // ---------------------------------------------------------------- handling: brakes, tyres, springs, weight, the engine's note

        private NwhVehicle Nwh()
        {
            if (_nwh == null)
            {
                var car = _vehicle as CarController;
                _nwh = car != null ? car.vehicleController : null;
            }
            return _nwh;
        }

        private void Handling(GarageModifiers m, bool wet)
        {
            ClearHandling();
            NwhVehicle nwh = Nwh();
            if (nwh == null || nwh.brakes == null || nwh.powertrain == null || nwh.powertrain.wheels == null || nwh.vehicleRigidbody == null)
            {
                return;
            }
            Rigidbody body = nwh.vehicleRigidbody;
            _brakeTorque = nwh.brakes.maxTorque;
            _mass = body.mass;
            var centre = _vehicle.GetComponent<NWH.Common.CoM.VariableCenterOfMass>();
            _baseMass = centre != null ? centre.baseMass : 0f;
            var note = nwh.soundManager != null ? nwh.soundManager.engineRunningComponent : null;
            if (note != null)
            {
                _pitch = note.pitchOffset;
                _volume = note.baseVolume;
            }
            _wheels.Clear();
            int count = nwh.powertrain.wheels.Count;
            float load = _mass * 9.81f / Mathf.Max(1, count);
            float grip = wet ? m.WetGrip : m.Grip;
            foreach (WheelComponent component in nwh.powertrain.wheels)
            {
                WheelUAPI wheel = component != null ? component.wheelUAPI : null;
                if (wheel == null)
                {
                    continue;
                }
                _wheels.Add(new WheelStock
                {
                    Wheel = wheel, Grip = wheel.LongitudinalFrictionGrip, SideGrip = wheel.LateralFrictionGrip,
                    SpringForce = wheel.SpringMaxForce, SpringLength = wheel.SpringMaxLength,
                    Bump = wheel.DamperBumpRate, Rebound = wheel.DamperReboundRate
                });
                wheel.LongitudinalFrictionGrip *= grip;
                wheel.LateralFrictionGrip *= grip;
                wheel.SpringMaxForce *= m.Stiffness;
                wheel.DamperBumpRate *= m.Stiffness;
                wheel.DamperReboundRate *= m.Stiffness;
                if (m.Ride != 0f && wheel.SpringMaxLength > 0.05f && wheel.SpringMaxForce > 1f)
                {
                    // Lowered only inside the travel the game's own suspension already uses under this car: the wheel
                    // rises into its arch by at most a quarter of what is left of the spring at rest, and never 5 cm.
                    float atRest = wheel.SpringMaxLength * (1f - Mathf.Clamp01(load / wheel.SpringMaxForce));
                    float ride = m.Ride < 0f
                        ? -Mathf.Min(-m.Ride, GarageCatalogue.MaxLower, atRest * 0.25f)
                        : Mathf.Min(m.Ride, GarageCatalogue.MaxRaise);
                    wheel.SpringMaxLength += ride;
                }
            }
            if (nwh.powertrain.wheelGroups != null)
            {
                _antiRoll = new float[nwh.powertrain.wheelGroups.Count];
                for (int i = 0; i < _antiRoll.Length; i++)
                {
                    _antiRoll[i] = nwh.powertrain.wheelGroups[i].antiRollBarForce;
                    nwh.powertrain.wheelGroups[i].antiRollBarForce = _antiRoll[i] * m.Stiffness;
                }
            }
            nwh.brakes.maxTorque = _brakeTorque * m.Brakes;
            if (m.Mass != 1f)
            {
                body.mass = _mass * m.Mass;
                if (centre != null)
                {
                    centre.baseMass = _baseMass * m.Mass;
                }
            }
            if (note != null)
            {
                note.pitchOffset = _pitch + m.Pitch;
                note.baseVolume = _volume * (1f + 0.25f * m.Loudness);
            }
            _handled = m;
            _wet = wet;
            _handling = true;
        }

        private void ClearHandling()
        {
            if (!_handling)
            {
                return;
            }
            _handling = false;
            NwhVehicle nwh = Nwh();
            if (nwh == null)
            {
                return;
            }
            try
            {
                for (int i = 0; i < _wheels.Count; i++)
                {
                    WheelStock stock = _wheels[i];
                    if (stock.Wheel == null)
                    {
                        continue;
                    }
                    stock.Wheel.LongitudinalFrictionGrip = stock.Grip;
                    stock.Wheel.LateralFrictionGrip = stock.SideGrip;
                    stock.Wheel.SpringMaxForce = stock.SpringForce;
                    stock.Wheel.SpringMaxLength = stock.SpringLength;
                    stock.Wheel.DamperBumpRate = stock.Bump;
                    stock.Wheel.DamperReboundRate = stock.Rebound;
                }
                _wheels.Clear();
                if (_antiRoll != null && nwh.powertrain != null && nwh.powertrain.wheelGroups != null)
                {
                    for (int i = 0; i < _antiRoll.Length && i < nwh.powertrain.wheelGroups.Count; i++)
                    {
                        nwh.powertrain.wheelGroups[i].antiRollBarForce = _antiRoll[i];
                    }
                }
                _antiRoll = null;
                if (nwh.brakes != null)
                {
                    nwh.brakes.maxTorque = _brakeTorque;
                }
                if (nwh.vehicleRigidbody != null && _mass > 0f)
                {
                    nwh.vehicleRigidbody.mass = _mass;
                    var centre = _vehicle.GetComponent<NWH.Common.CoM.VariableCenterOfMass>();
                    if (centre != null && _baseMass > 0f)
                    {
                        centre.baseMass = _baseMass;
                    }
                }
                var note = nwh.soundManager != null ? nwh.soundManager.engineRunningComponent : null;
                if (note != null)
                {
                    note.pitchOffset = _pitch;
                    note.baseVolume = _volume;
                }
            }
            catch (Exception e)
            {
                Logger?.Warn("Garage: a car's handling could not be put back completely. " + e);
            }
        }

        /// <summary>The car's weight and brakes as the game made them, whatever is fitted now: what the shop's figures start from.</summary>
        public void StockFigures(out float mass, out float brakeTorque)
        {
            NwhVehicle nwh = Nwh();
            mass = _handling ? _mass : nwh != null && nwh.vehicleRigidbody != null ? nwh.vehicleRigidbody.mass : 0f;
            brakeTorque = _handling ? _brakeTorque : nwh != null && nwh.brakes != null ? nwh.brakes.maxTorque : 0f;
        }

        private static bool Same(in GarageModifiers a, in GarageModifiers b)
        {
            return a.Brakes == b.Brakes && a.Grip == b.Grip && a.WetGrip == b.WetGrip && a.Stiffness == b.Stiffness
                   && a.Mass == b.Mass && a.Ride == b.Ride && a.Pitch == b.Pitch && a.Loudness == b.Loudness;
        }
    }
}
