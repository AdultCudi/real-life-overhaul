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
    /// One of the player's cars wearing its build, on the car's own object. Nothing per frame: all is done once as the
    /// build changes, through the property blocks the game paints cars with, everything touched recorded first so that
    /// <see cref="Clear"/> leaves the car as the game made it.
    /// </summary>
    public sealed partial class GarageCar : MonoBehaviour
    {
        // The vehicle shader's generated property names.
        private static readonly int TintId = Shader.PropertyToID("Color_3d0f0cdbe6b74be28a1a5be5bab71dea");
        private static readonly int FresnelColourId = Shader.PropertyToID("Color_f78fac473bac467092fb27521e9f71ea");
        private static readonly int FresnelPowerId = Shader.PropertyToID("Vector1_481fa2a8a5e94165a039319bfd512b76");
        private static readonly int GlossId = Shader.PropertyToID("Vector1_41991b689ccf4932ad504975ac34399a");
        private static readonly int LampColourId = Shader.PropertyToID("_EmissionColor");

        private const float WheelPropRadius = 0.34f;   // metres: the size the wheel props are modelled at
        private const float WheelPropWidth = 0.22f;    // metres
        private const float GlowLumens = 900f;         // lumen

        public static IModLogger Logger;

        private static MaterialPropertyBlock _block;
        private static readonly List<Light> LightScratch = new List<Light>(4);

        private struct WheelStock
        {
            public WheelUAPI Wheel;
            public float Grip, SideGrip, SpringForce, SpringLength, Bump, Rebound, Application;
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
        private bool _lifted;                  // the handling parts are off for a moment, between LiftHandling and LayHandling
        private GarageModifiers _liftedParts;
        private bool _liftedWet;

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
            _lifted = false;
            ClearHandling();
            _shown.Take(new GarageBuild());
            _any = false;
        }

        private void OnDestroy()
        {
            // Nothing outlives the car: the pieces are its children, the meshes and materials shared.
            _parts = null;
        }

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
                    float foot = fit.DeckPostHeight - 0.015f;   // sunk a little into the deck: no gap under a foot
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
                return;   // not in the bundle: the car keeps its own
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
                // Sized to the car's wheel: the radius from the physics, the width from its mesh.
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
            float grip = wet ? m.WetGrip : m.Grip;
            float stiffness = m.Stiffness > 0.1f ? m.Stiffness : 1f;
            float weight = m.Mass > 0.1f ? m.Mass : 1f;
            float damping = Mathf.Sqrt(stiffness * weight);   // the car keeps its damping ratio
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
                    Bump = wheel.DamperBumpRate, Rebound = wheel.DamperReboundRate, Application = wheel.ForceApplicationPointDistance
                });
                wheel.LongitudinalFrictionGrip *= grip;
                wheel.LateralFrictionGrip *= grip;
                if (wheel.SpringMaxLength > 0.05f && wheel.SpringMaxForce > 1f)
                {
                    // On the car's own spring and curve, never into the travel a kerb needs; the roll centre moves with it.
                    float load = WheelLoad(nwh, wheel, body);   // before the weight part, which the fit takes in
                    HandlingModel.FitSpring(CurveOf(wheel), wheel.SpringMaxForce, wheel.SpringMaxLength, load, weight, stiffness, m.Ride, GarageCatalogue.MaxLower,
                        GarageCatalogue.MaxRaise, out float force, out float length, out float change);
                    float rollCentre = wheel.ForceApplicationPointDistance * wheel.SpringMaxLength + change;
                    wheel.SpringMaxForce = force;
                    wheel.SpringMaxLength = length;
                    wheel.ForceApplicationPointDistance = rollCentre / length;
                }
                wheel.DamperBumpRate *= damping;
                wheel.DamperReboundRate *= damping;
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
                    stock.Wheel.ForceApplicationPointDistance = stock.Application;
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

        /// <summary>For the driven car's handling: the parts' handling off for a moment. True if any was on.</summary>
        public bool LiftHandling()
        {
            if (!_handling)
            {
                return false;
            }
            _liftedParts = _handled;
            _liftedWet = _wet;
            ClearHandling();
            _lifted = true;
            return true;
        }

        /// <summary>For Driving: what <see cref="LiftHandling"/> took off, put back on top of whatever is under it now. Nothing if it took nothing.</summary>
        public void LayHandling()
        {
            if (!_lifted)
            {
                return;
            }
            _lifted = false;
            Handling(_liftedParts, _liftedWet);
        }

        // The load on a wheel at rest, shared between the axles by the centre of mass (evenly if all stand one side).
        private static float WheelLoad(NwhVehicle nwh, WheelUAPI wheel, Rigidbody body)
        {
            List<WheelComponent> wheels = nwh.powertrain.wheels;
            Transform space = nwh.transform;
            float com = body.centerOfMass.z;
            float frontZ = 0f, rearZ = 0f;
            int front = 0, rear = 0;
            for (int i = 0; i < wheels.Count; i++)
            {
                WheelUAPI other = wheels[i] != null ? wheels[i].wheelUAPI : null;
                if (other == null)
                {
                    continue;
                }
                float z = space.InverseTransformPoint(other.transform.position).z;
                if (z > com)
                {
                    frontZ += z;
                    front++;
                }
                else
                {
                    rearZ += z;
                    rear++;
                }
            }
            float weight = body.mass * 9.81f;
            if (front == 0 || rear == 0)
            {
                return weight / Mathf.Max(1, front + rear);
            }
            frontZ /= front;
            rearZ /= rear;
            float own = space.InverseTransformPoint(wheel.transform.position).z;
            return own > com ? weight * (com - rearZ) / (frontZ - rearZ) / front : weight * (frontZ - com) / (frontZ - rearZ) / rear;
        }

        // The wheel's own spring curve, compression to force share; a straight line where it has none of the game's.
        private static Func<float, float> CurveOf(WheelUAPI wheel)
        {
            AnimationCurve curve = wheel is NWH.WheelController3D.WheelController controller && controller.spring != null ? controller.spring.forceCurve : null;
            if (curve == null)
            {
                return c => c;
            }
            return curve.Evaluate;
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
