using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The haze of the game's street lamps: the glow object each lamp switches with its light (StreetLamp.volumetricLightObject,
    /// the game's own cone) and the volumetric scatter of its lights. Two jobs, both about hair seen against a lit lamp:
    ///
    ///  - <see cref="GlowQueue"/> reads once per scene which transparent queue the game draws that glow in, so the hair finish
    ///    and the mod's own hair looks can be sorted after it (<see cref="HairFinish.QueueAfter"/>): hair drawn before a cone
    ///    that is blended over it is covered by it, hair drawn after it lies on top of it.
    ///  - The option "Street light haze" (<see cref="Settings.LampHaze"/>): off hides the glow and takes the lights out of the
    ///    volumetric fog, and the lights themselves stay as they are. It is the way out for a player on whom a cone still covers
    ///    hair after the sorting, e.g. a cone the game draws in the low resolution pass, which no hair material can be sorted into.
    ///
    /// The game switches the glow object on and off with SetActive when a lamp is lit or put out, so the glow is hidden by
    /// disabling its renderers (and any local volumetric fog it carries), never by deactivating it. Everything changed is
    /// remembered and put back exactly.
    ///
    /// Per frame: nothing. A change of the option goes through the lamps of the game's culling list once; while the haze is off,
    /// <see cref="Tick"/> (every few seconds, from CharacterQuality's timer) looks at the list for lamps that have arrived since.
    /// </summary>
    internal static class LampHaze
    {
        private sealed class Hidden
        {
            public Renderer[] Renderers;
            public bool[] RendererWas;
            public LocalVolumetricFog[] Fogs;
            public bool[] FogWas;
            public HDAdditionalLightData[] Lights;
            public bool[] LightWas;
        }

        private static readonly Dictionary<int, Hidden> Kept = new Dictionary<int, Hidden>(256);
        private static readonly List<StreetLamp> Lamps = new List<StreetLamp>(4096);
        private static readonly int ZWriteId = Shader.PropertyToID("_ZWrite");
        private static readonly int TransparentZWriteId = Shader.PropertyToID("_TransparentZWrite");
        private static readonly int SortPriorityId = Shader.PropertyToID("_TransparentSortPriority");

        private static bool _found;      // a lamp with a glow was read this scene
        private static int _queue;       // the highest render queue among the glow's materials; 0 = it has no mesh
        private static string _said = "no street lamp has been looked at yet";

        /// <summary>
        /// The render queue the game draws its street lamp glow in, or 0 where it has none or none was found yet. Read from the
        /// game's culling list once a scene (and again only while nothing was found); no allocation after the first.
        /// </summary>
        public static int GlowQueue()
        {
            if (!_found)
            {
                Look();
            }
            return _queue;
        }

        /// <summary>What was read of the glow and what the haze option has done, for the hair check line.</summary>
        public static string Describe()
        {
            return _said + "; option " + (Settings.LampHaze ? "on" : "off, " + Kept.Count + " lamps' glow hidden");
        }

        private static void Look()
        {
            if (!PerfGame.KnowsLamps)
            {
                _said = "this version of the game keeps a street lamp's glow somewhere else";
                return;
            }
            PerfGame.Lamps(Lamps);
            for (int i = 0; i < Lamps.Count && !_found; i++)
            {
                GameObject glow = PerfGame.GlowOf(Lamps[i]);
                if (glow == null)
                {
                    continue;
                }
                _found = true;
                Renderer[] renderers = glow.GetComponentsInChildren<Renderer>(true);
                int fogs = glow.GetComponentsInChildren<LocalVolumetricFog>(true).Length;
                var text = new StringBuilder(220);
                text.Append("lamp glow '").Append(glow.name).Append("': ").Append(renderers.Length).Append(" renderer(s), ")
                    .Append(fogs).Append(" local volumetric fog(s)");
                for (int r = 0; r < renderers.Length; r++)
                {
                    Material[] materials = renderers[r].sharedMaterials;
                    for (int m = 0; m < materials.Length; m++)
                    {
                        Material material = materials[m];
                        if (material == null)
                        {
                            continue;
                        }
                        _queue = Mathf.Max(_queue, material.renderQueue);
                        if (r == 0)
                        {
                            text.Append("; shader '").Append(material.shader != null ? material.shader.name : "none").Append("' queue ")
                                .Append(material.renderQueue).Append(" (").Append(HairFinish.PassOf(material.renderQueue)).Append(" pass), writes depth ")
                                .Append(Flag(material, ZWriteId) ?? Flag(material, TransparentZWriteId) ?? "unknown").Append(", sort ")
                                .Append(material.HasProperty(SortPriorityId) ? material.GetFloat(SortPriorityId).ToString("F0") : "none");
                        }
                    }
                }
                _said = text.ToString();
            }
            Lamps.Clear();
            if (!_found)
            {
                _said = "no street lamp with a glow was found yet";
            }
        }

        private static string Flag(Material material, int id)
        {
            return material.HasProperty(id) ? (material.GetFloat(id) > 0.5f ? "yes" : "no") : null;
        }

        /// <summary>Makes the lamps match the option: hides their haze, or puts it back. Safe to call again and again.</summary>
        public static void Apply(bool on)
        {
            if (on)
            {
                Restore();
            }
            else
            {
                Sweep();
            }
        }

        /// <summary>While the haze is off: the lamps that have arrived since the last look lose theirs too.</summary>
        public static void Tick()
        {
            if (!Settings.LampHaze)
            {
                Sweep();
            }
        }

        private static void Sweep()
        {
            if (!PerfGame.KnowsLamps)
            {
                return;
            }
            PerfGame.Lamps(Lamps);
            for (int i = 0; i < Lamps.Count; i++)
            {
                StreetLamp lamp = Lamps[i];
                int id = lamp.GetInstanceID();
                if (!Kept.ContainsKey(id))
                {
                    Kept[id] = Hide(lamp);
                }
            }
            Lamps.Clear();
        }

        private static Hidden Hide(StreetLamp lamp)
        {
            var hidden = new Hidden();
            GameObject glow = PerfGame.GlowOf(lamp);
            hidden.Renderers = glow != null ? glow.GetComponentsInChildren<Renderer>(true) : new Renderer[0];
            hidden.Fogs = glow != null ? glow.GetComponentsInChildren<LocalVolumetricFog>(true) : new LocalVolumetricFog[0];
            hidden.Lights = PerfGame.LightDataOf(lamp) ?? new HDAdditionalLightData[0];
            hidden.RendererWas = new bool[hidden.Renderers.Length];
            hidden.FogWas = new bool[hidden.Fogs.Length];
            hidden.LightWas = new bool[hidden.Lights.Length];
            for (int i = 0; i < hidden.Renderers.Length; i++)
            {
                hidden.RendererWas[i] = hidden.Renderers[i] != null && hidden.Renderers[i].enabled;
                if (hidden.Renderers[i] != null)
                {
                    hidden.Renderers[i].enabled = false;
                }
            }
            for (int i = 0; i < hidden.Fogs.Length; i++)
            {
                hidden.FogWas[i] = hidden.Fogs[i] != null && hidden.Fogs[i].enabled;
                if (hidden.Fogs[i] != null)
                {
                    hidden.Fogs[i].enabled = false;
                }
            }
            for (int i = 0; i < hidden.Lights.Length; i++)
            {
                hidden.LightWas[i] = hidden.Lights[i] != null && hidden.Lights[i].affectsVolumetric;
                if (hidden.Lights[i] != null)
                {
                    hidden.Lights[i].affectsVolumetric = false;
                }
            }
            return hidden;
        }

        private static void Restore()
        {
            foreach (Hidden hidden in Kept.Values)
            {
                for (int i = 0; i < hidden.Renderers.Length; i++)
                {
                    if (hidden.Renderers[i] != null)
                    {
                        hidden.Renderers[i].enabled = hidden.RendererWas[i];
                    }
                }
                for (int i = 0; i < hidden.Fogs.Length; i++)
                {
                    if (hidden.Fogs[i] != null)
                    {
                        hidden.Fogs[i].enabled = hidden.FogWas[i];
                    }
                }
                for (int i = 0; i < hidden.Lights.Length; i++)
                {
                    if (hidden.Lights[i] != null)
                    {
                        hidden.Lights[i].affectsVolumetric = hidden.LightWas[i];
                    }
                }
            }
            Kept.Clear();
        }

        /// <summary>End of the scene: put back what is still there, forget what was read.</summary>
        public static void Forget()
        {
            Restore();
            _found = false;
            _queue = 0;
            _said = "no street lamp has been looked at yet";
        }
    }
}
