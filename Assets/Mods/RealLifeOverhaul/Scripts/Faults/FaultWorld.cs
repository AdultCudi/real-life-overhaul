using System;
using System.Collections.Generic;
using System.Reflection;
using GleyTrafficSystem;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

namespace RealLifeOverhaul.Faults
{
    /// <summary>
    /// The city's faults as the player sees them, alike on every machine. A dark lamp is handed empty lists of lights, so
    /// the game's own switching finds nothing to light, and gets its own back when mended. A pothole is a prop and a
    /// slower lane point; litter, an unsafe place, pooled props stood within <see cref="ShowWithin"/> metres; a hot spot
    /// shows nothing. All is put back as found (<see cref="Clear"/>). Cost: nothing per frame; <see cref="Sync"/> is a
    /// pass over a dozen faults when they change and every few seconds.
    /// </summary>
    internal sealed class FaultWorld
    {
        private const float ShowWithin = 250f;        // metres: props stood for faults this near the player
        private const float LaneOnHole = 3f;         // metres: the lane point a pothole slows is this near it
        private const float PileSpread = 0.7f;       // metres: litter's heaps round the middle
        private const float LidAside = 0.95f, ConeAside = 0.8f, ConeAlong = 0.6f;   // metres: an open manhole's lid and cone
        private const float SignLying = 0.38f;       // metres: a fallen works sign's middle above the pavement (half its board)
        private const float HoleLift = 0.006f;       // metres above the road: no flicker with its surface

        private static readonly FieldInfo LampLights = GameContract.LampOutLights.Field;
        private static readonly FieldInfo LampData = GameContract.LampOutData.Field;
        private static readonly FieldInfo LampGlow = GameContract.LampOutGlow.Field;
        private static readonly FieldInfo LampIsOn = GameContract.LampOutIsOn.Field;
        private static readonly FieldInfo LampHead = GameContract.LampOutHead.Field;
        private static readonly FieldInfo LampHeads = GameContract.LampOutHeads.Field;
        private static readonly FieldInfo LampEmission = GameContract.LampOutEmission.Field;
        private static readonly int EmissiveExposureWeight = Shader.PropertyToID("_EmissiveExposureWeight");   // the game's own (StreetLamp.ToggleEmission)
        private static readonly Light[] NoLights = new Light[0];
        private static readonly HDAdditionalLightData[] NoData = new HDAdditionalLightData[0];
        private static MaterialPropertyBlock _block;

        /// <summary>False if this version of the game keeps a lamp's lights somewhere else: no lamp goes dark, the other faults go on.</summary>
        private static bool KnowsLamps => LampLights != null && LampData != null && LampGlow != null && LampIsOn != null && LampHead != null && LampHeads != null && LampEmission != null;

        private sealed class Shown
        {
            public int Id;
            public FaultKind Kind;
            public Vector3 At;
            public float Heading;
            public int Variant;
            public readonly List<Transform> Props = new List<Transform>(3);
            public StreetLamp Lamp;
            public Light[] Lights;
            public HDAdditionalLightData[] Data;
            public GameObject Glow;
            public int Emission = -1;
            public Waypoint Lane;
            public int LaneLimit;
            public bool LaneTried;
        }

        private readonly Dictionary<int, Shown> _shown = new Dictionary<int, Shown>(16);
        private readonly List<int> _gone = new List<int>(8);
        private readonly CivicProps _props;
        private readonly Transform _parent;

        public FaultWorld(Transform parent)
        {
            _parent = parent;
            _props = new CivicProps(parent);
        }

        /// <summary>The world brought in line with the faults: the mended put back, the new switched and, near the player, stood.</summary>
        public void Sync(FaultFile file, bool hasPlayer, Vector3 player)
        {
            _gone.Clear();
            foreach (KeyValuePair<int, Shown> pair in _shown)
            {
                if (file == null || file.Find(pair.Key) == null)
                {
                    _gone.Add(pair.Key);
                }
            }
            for (int i = 0; i < _gone.Count; i++)
            {
                Hide(_shown[_gone[i]]);
                _shown.Remove(_gone[i]);
            }
            for (int i = 0; file != null && i < file.Faults.Count; i++)
            {
                Fault f = file.Faults[i];
                if (!_shown.TryGetValue(f.Id, out Shown s))
                {
                    s = new Shown { Id = f.Id, Kind = f.Kind, At = new Vector3(f.X, f.Y, f.Z), Heading = f.Heading, Variant = f.Variant };
                    _shown[f.Id] = s;
                }
                if (s.Kind == FaultKind.Lamp && s.Lamp == null)
                {
                    Darken(s);
                }
                else if (s.Kind == FaultKind.Lamp)
                {
                    KeepDark(s);
                }
                if (s.Kind == FaultKind.Pothole && !s.LaneTried)
                {
                    Slow(s);
                }
                bool near = hasPlayer && Flat(s.At - player) <= ShowWithin * ShowWithin;
                if (near && s.Props.Count == 0)
                {
                    Stand(s);
                }
                else if (!near && s.Props.Count > 0)
                {
                    _props.ReturnAll(s.Props);
                }
            }
        }

        /// <summary>Everything put back as it was found: lamps lit again by the game's own call, lanes at their limits, props on the shelf.</summary>
        public void Clear()
        {
            foreach (Shown s in _shown.Values)
            {
                try
                {
                    Hide(s);
                }
                catch (Exception e)
                {
                    FaultDirector.Logger?.Error(e);   // (one lamp or prop that cannot be put back never leaves the others as they were)
                }
            }
            _shown.Clear();
        }

        /// <summary>Props and their meshes gone for good (the director is destroyed). Clear first.</summary>
        public void Dispose()
        {
            Clear();
            _props.Dispose();
        }

        private void Hide(Shown s)
        {
            _props.ReturnAll(s.Props);
            if (s.Lamp != null || s.Lights != null)
            {
                Relight(s);
            }
            if (s.Lane != null)
            {
                s.Lane.maxSpeed = s.LaneLimit;
                s.Lane = null;
            }
        }

        private void Darken(Shown s)
        {
            if (!KnowsLamps)
            {
                return;
            }
            StreetLamp lamp = FaultGame.LampAt(s.At);
            if (lamp == null)
            {
                return;   // (the lamps not read yet: tried again at the next sync)
            }
            if (ReferenceEquals(LampLights.GetValue(lamp), NoLights))
            {
                return;   // dark already (another fault of the same lamp): never darkened twice, or the empty lists would be "put back"
            }
            s.Lamp = lamp;
            s.Lights = LampLights.GetValue(lamp) as Light[];
            s.Data = LampData.GetValue(lamp) as HDAdditionalLightData[];
            s.Glow = LampGlow.GetValue(lamp) as GameObject;
            s.Emission = (int)LampEmission.GetValue(lamp);
            LampLights.SetValue(lamp, NoLights);   // the game's switching (dusk, coming near) now has nothing to light
            LampData.SetValue(lamp, NoData);
            LampGlow.SetValue(lamp, null);
            LampEmission.SetValue(lamp, -1);
            for (int i = 0; s.Lights != null && i < s.Lights.Length; i++)
            {
                if (s.Lights[i] != null)
                {
                    s.Lights[i].enabled = false;
                }
            }
            for (int i = 0; s.Data != null && i < s.Data.Length; i++)
            {
                if (s.Data[i] != null)
                {
                    s.Data[i].enabled = false;
                }
            }
            if (s.Glow != null)
            {
                s.Glow.SetActive(false);
            }
            if (s.Emission >= 0)
            {
                Glow(lamp, false);
            }
        }

        /// <summary>Every dark lamp's lights switched off again now.</summary>
        public void KeepDark()
        {
            foreach (Shown s in _shown.Values)
            {
                if (s.Kind == FaultKind.Lamp && s.Lamp != null)
                {
                    KeepDark(s);
                }
            }
        }

        // Switches off again any light of a dark lamp something else turned on. A few enabled tests.
        private static void KeepDark(Shown s)
        {
            for (int i = 0; s.Lights != null && i < s.Lights.Length; i++)
            {
                if (s.Lights[i] != null && s.Lights[i].enabled)
                {
                    s.Lights[i].enabled = false;
                }
            }
            for (int i = 0; s.Data != null && i < s.Data.Length; i++)
            {
                if (s.Data[i] != null && s.Data[i].enabled)
                {
                    s.Data[i].enabled = false;
                }
            }
            if (s.Glow != null && s.Glow.activeSelf)
            {
                s.Glow.SetActive(false);
            }
        }

        // The lamp's own lists back, unlit as the game keeps one, then lit by the game's own call if it should be now.
        private void Relight(Shown s)
        {
            StreetLamp lamp = s.Lamp;
            if (lamp != null)
            {
                LampLights.SetValue(lamp, s.Lights ?? NoLights);
                LampData.SetValue(lamp, s.Data ?? NoData);
                LampGlow.SetValue(lamp, s.Glow);
                LampEmission.SetValue(lamp, s.Emission);
                LampIsOn.SetValue(lamp, false);
                if (lamp.isActiveAndEnabled && !GameManager.isCitySceneBeingUnloaded && InstanceBehavior<GameManager>.Instance != null)
                {
                    Light(lamp);   // (not while the city unloads: the game's own call reads its time of day then gone)
                }
            }
            s.Lamp = null;
            s.Lights = null;
            s.Data = null;
            s.Glow = null;
        }

        // (A call of the game's own in a method of its own: Mono compiles a method whole.)
        private static void Light(StreetLamp lamp)
        {
            lamp.ChangeLights(true);
        }

        // The lamp head's glow, as the game's StreetLamp.ToggleEmission sets it.
        private static void Glow(StreetLamp lamp, bool on)
        {
            _block ??= new MaterialPropertyBlock();
            MeshRenderer head = LampHead.GetValue(lamp) as MeshRenderer;
            if (head != null)
            {
                head.GetPropertyBlock(_block);
                _block.SetFloat(EmissiveExposureWeight, on ? 0f : 1f);
                head.SetPropertyBlock(_block);
            }
            if (LampHeads.GetValue(lamp) is MeshRenderer[] heads)
            {
                for (int i = 0; i < heads.Length; i++)
                {
                    if (heads[i] != null)
                    {
                        heads[i].GetPropertyBlock(_block);
                        _block.SetFloat(EmissiveExposureWeight, on ? 0f : 1f);
                        heads[i].SetPropertyBlock(_block);
                    }
                }
            }
        }

        private void Slow(Shown s)
        {
            if (!TrafficTools.Street || !RoadMap.Ensure())
            {
                return;   // (traffic not running yet: tried again at the next sync)
            }
            s.LaneTried = true;
            if (!RoadMap.NearestLane(s.At, out float distance, out _, out _, out _, out Waypoint point) || point == null || distance > LaneOnHole)
            {
                return;
            }
            foreach (Shown other in _shown.Values)
            {
                if (other != s && other.Lane == point)
                {
                    return;   // slowed already by another hole
                }
            }
            s.Lane = point;
            s.LaneLimit = point.maxSpeed;
            point.maxSpeed = FaultRules.SlowLimit(point.maxSpeed);
        }

        private void Stand(Shown s)
        {
            Vector3 forward = Quaternion.Euler(0f, s.Heading, 0f) * Vector3.forward;
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            switch (s.Kind)
            {
                case FaultKind.Pothole:
                    Put(s, "pothole", s.At + Vector3.up * HoleLift, forward);
                    break;
                case FaultKind.Litter:
                    for (int k = 0; k < 3; k++)
                    {
                        float angle = (s.Variant * 37 + k * 120) % 360;
                        Vector3 offset = Quaternion.Euler(0f, angle, 0f) * Vector3.forward * (PileSpread * (0.4f + 0.3f * k));
                        Put(s, "litter_pile", s.At + offset, Quaternion.Euler(0f, angle * 2f, 0f) * forward);
                    }
                    break;
                case FaultKind.Unsafe:
                    if (s.Variant % 2 == 0)
                    {
                        Put(s, "manhole_open", s.At, forward);
                        Put(s, "manhole_lid", s.At + right * LidAside, forward);
                        Put(s, "cone", s.At - right * ConeAside + forward * ConeAlong, forward);
                    }
                    else
                    {
                        Transform sign = Put(s, "road_sign_works", s.At + Vector3.up * SignLying, forward);
                        if (sign != null)
                        {
                            sign.rotation = Quaternion.LookRotation(forward) * Quaternion.Euler(0f, 0f, 90f);   // fallen on its side
                        }
                    }
                    break;
            }
        }

        private Transform Put(Shown s, string id, Vector3 at, Vector3 forward)
        {
            Transform prop = _props.Stand(id, at, forward);
            if (prop != null)
            {
                s.Props.Add(prop);
            }
            return prop;
        }

        /// <summary>True if a wheel's path from one point to another goes over a pothole near the player.</summary>
        public bool OverHole(Vector3 from, Vector3 to)
        {
            foreach (Shown s in _shown.Values)
            {
                if (s.Kind == FaultKind.Pothole && Mathf.Abs(s.At.y - to.y) < 3f && FaultRules.Over(from.x, from.z, to.x, to.z, s.At.x, s.At.z))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>The unsafe place nearest a point within so many metres, if any.</summary>
        public bool UnsafeNear(Vector3 point, float within, out Vector3 spot)
        {
            spot = default;
            float best = within * within;
            bool found = false;
            foreach (Shown s in _shown.Values)
            {
                float sqr = Flat(s.At - point);
                if (s.Kind == FaultKind.Unsafe && sqr <= best)
                {
                    best = sqr;
                    spot = s.At;
                    found = true;
                }
            }
            return found;
        }

        private static float Flat(Vector3 v)
        {
            return v.x * v.x + v.z * v.z;
        }
    }
}
