using System;
using System.Collections.Generic;
using BAModAPI;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

namespace RealLifeOverhaul
{
    /// <summary>
    /// Softer lighting for one character on show: character creation, and the preview in the
    /// hairdresser, clothes shop and plastic surgery screens.
    ///
    /// The game lights these with two hard-shadowed white lights, which carves the face up.
    /// This keeps those two as they are placed (they are the key), makes their shadows less
    /// black, and adds the two lights a portrait set-up normally has: a weak fill from the
    /// camera's side opposite the key, to open up the shadow side of the face, and a rim light
    /// from behind to separate hair and shoulders from the backdrop. Both are copies of one of
    /// the game's own lights, so they are in the same units and reach the same things, with
    /// their shadows switched off. Colours stay within a few percent of white.
    ///
    /// Applied once; nothing here runs per frame. <see cref="Restore"/> undoes all of it.
    /// </summary>
    internal sealed class CharacterLighting
    {
        private const float ShadowStrength = 0.6f;   // 1 = the game's black shadows
        private const float FillIntensity = 0.45f;   // of the light it is copied from
        private const float RimIntensity = 0.9f;
        private const float FillAngle = 50f;         // degrees round from the camera, away from the key
        private const float RimAngle = 140f;
        private const float ChestHeight = 1.35f;     // metres above the character's feet

        private static readonly Color FillColour = new Color(0.94f, 0.96f, 1f);
        private static readonly Color RimColour = new Color(1f, 0.97f, 0.94f);

        private struct Softened
        {
            public HDAdditionalLightData Light;
            public float ShadowDimmer;
        }

        private readonly IModLogger _logger;
        private readonly string _label;
        private readonly Light[] _lights;
        private readonly Transform _character;
        private readonly Transform _camera;
        private readonly Transform _parent;
        private readonly List<Softened> _softened = new List<Softened>(4);
        private readonly List<GameObject> _added = new List<GameObject>(2);

        public bool Applied { get; private set; }
        public int AddedLights => _added.Count;
        public int SoftenedLights => _softened.Count;

        /// <param name="lights">The game's lights on this character.</param>
        /// <param name="camera">The camera the character is shown through; may be null.</param>
        /// <param name="parent">What the added lights hang under, so that they come and go with the set-up.</param>
        public CharacterLighting(string label, Light[] lights, Transform character, Transform camera, Transform parent, IModLogger logger)
        {
            _label = label;
            _lights = lights ?? Array.Empty<Light>();
            _character = character;
            _camera = camera;
            _parent = parent;
            _logger = logger;
        }

        public void Apply()
        {
            if (Applied)
            {
                return;
            }
            Applied = true;
            if (_character == null || _parent == null)
            {
                return;
            }

            Light sun = null;      // a directional light, if the set-up has one: it is the stronger of the two
            Light local = null;    // a spot or point light to copy
            for (int i = 0; i < _lights.Length; i++)
            {
                Light light = _lights[i];
                if (light == null)
                {
                    continue;
                }
                var data = light.GetComponent<HDAdditionalLightData>();
                if (data == null)
                {
                    continue;
                }
                if (light.shadows != LightShadows.None && data.shadowDimmer > ShadowStrength)
                {
                    _softened.Add(new Softened { Light = data, ShadowDimmer = data.shadowDimmer });
                    data.shadowDimmer = ShadowStrength;
                }
                if (light.type == LightType.Directional)
                {
                    if (sun == null || light.intensity > sun.intensity)
                    {
                        sun = light;
                    }
                }
                else if (light.type == LightType.Spot || light.type == LightType.Point)
                {
                    if (local == null || (light.type == LightType.Spot && local.type != LightType.Spot))
                    {
                        local = light;
                    }
                }
            }

            if (local == null)
            {
                _logger?.Info("Character lighting (" + _label + "): shadows softened on " + _softened.Count + " lights; no spot or point light to copy, so no fill or rim was added.");
                return;
            }

            Vector3 subject = _character.position + Vector3.up * ChestHeight;

            // "Front" is towards the camera, level with the ground.
            Vector3 front = _camera != null ? _camera.position - subject : _character.forward;
            front.y = 0f;
            if (front.sqrMagnitude < 0.09f)
            {
                front = _character.forward;
                front.y = 0f;
            }
            if (front.sqrMagnitude < 1e-4f)
            {
                front = Vector3.forward;
            }
            front.Normalize();
            Vector3 side = Vector3.Cross(Vector3.up, front);

            // Which side the key comes from; fill and rim go on the other.
            Vector3 keyFrom = sun != null ? -sun.transform.forward : local.transform.position - subject;
            float away = Vector3.Dot(keyFrom, side) >= 0f ? -1f : 1f;
            float distance = Mathf.Clamp(Vector3.Distance(local.transform.position, subject), 1.5f, 5f);

            Add(local, "RealLifeOverhaul Fill Light", subject, Direction(front, side * away, FillAngle), distance, 0.1f, FillIntensity, FillColour);
            Add(local, "RealLifeOverhaul Rim Light", subject, Direction(front, side * away, RimAngle), distance, 0.8f, RimIntensity, RimColour);
            _logger?.Info("Character lighting (" + _label + "): shadows softened on " + _softened.Count + " lights, " + _added.Count + " shadowless lights added, copied from '" + local.name + "'.");
        }

        private static Vector3 Direction(Vector3 front, Vector3 side, float degrees)
        {
            float radians = degrees * Mathf.Deg2Rad;
            return front * Mathf.Cos(radians) + side * Mathf.Sin(radians);
        }

        private void Add(Light source, string name, Vector3 subject, Vector3 direction, float distance, float lift, float intensity, Color colour)
        {
            // Copied while inactive, so that nothing else on the game's light object wakes up in the copy.
            var holder = new GameObject("RealLifeOverhaul Light Holder");
            holder.SetActive(false);
            holder.transform.SetParent(_parent, false);
            GameObject copy = null;
            try
            {
                copy = UnityEngine.Object.Instantiate(source.gameObject, holder.transform);
                copy.name = name;
                for (int i = copy.transform.childCount - 1; i >= 0; i--)
                {
                    UnityEngine.Object.DestroyImmediate(copy.transform.GetChild(i).gameObject);
                }
                CharacterTools.Strip(copy, KeepOnLight, true);

                var light = copy.GetComponent<Light>();
                var data = copy.GetComponent<HDAdditionalLightData>();
                if (light == null || data == null)
                {
                    UnityEngine.Object.DestroyImmediate(copy);
                    return;
                }
                light.shadows = LightShadows.None;
                light.color = colour;
                light.enabled = true;
                data.intensity = data.intensity * intensity;   // same unit as the light it was copied from
                data.affectsVolumetric = false;

                Transform transform = copy.transform;
                transform.SetParent(_parent, true);
                transform.position = subject + direction * distance + Vector3.up * lift;
                transform.rotation = Quaternion.LookRotation(subject - transform.position, Vector3.up);
                copy.SetActive(true);
                _added.Add(copy);
            }
            catch (Exception e)
            {
                _logger?.Error(e);
                if (copy != null)
                {
                    UnityEngine.Object.Destroy(copy);
                }
            }
            finally
            {
                UnityEngine.Object.Destroy(holder);
            }
        }

        private static bool KeepOnLight(MonoBehaviour script)
        {
            return script is HDAdditionalLightData;
        }

        public void Restore()
        {
            if (!Applied)
            {
                return;
            }
            Applied = false;
            for (int i = 0; i < _softened.Count; i++)
            {
                if (_softened[i].Light != null)
                {
                    _softened[i].Light.shadowDimmer = _softened[i].ShadowDimmer;
                }
            }
            _softened.Clear();
            for (int i = 0; i < _added.Count; i++)
            {
                if (_added[i] != null)
                {
                    UnityEngine.Object.Destroy(_added[i]);
                }
            }
            _added.Clear();
        }
    }
}
