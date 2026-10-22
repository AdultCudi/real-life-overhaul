using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

namespace RealLifeOverhaul
{
    /// <summary>The looks of <see cref="GarageCar"/>: paint and finish, lamp tint and underglow.</summary>
    public sealed partial class GarageCar
    {
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
                // The car's own lamps, found once; never the underglow.
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

        /// <summary>The underglow on or off, by night, the option and the shop; does anything only on a change.</summary>
        public void LightGlow(bool on)
        {
            if (_glow != null && _glow.gameObject.activeSelf != on)
            {
                _glow.gameObject.SetActive(on);
            }
        }

        public bool HasGlow => _glow != null;
    }
}
