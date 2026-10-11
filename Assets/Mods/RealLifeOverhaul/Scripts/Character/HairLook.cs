using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using BAModAPI;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// Puts the hair finish (<see cref="HairFinish"/>: solid strand cores through the depth prepass, strand maps,
    /// gloss and back-light that follow the hair colour and are bounded against lit skin) on the hair and beard of
    /// the detailed characters the mod looks after: the player's double in the city, the character being made in
    /// character creation, and whoever the shop preview shows (hairdresser, clothing store, plastic surgeon).
    ///
    /// Work is done only when a character's look changes: each layer of the worn hair is swapped for a finished
    /// copy, and when only the colour changed the copies are re-tinted. The game writes a new hair colour either
    /// straight onto what is on the renderer (ours) or through Renderer.materials, which leaves its own clone of
    /// ours there; a clone of a finished layer is recognised by its name and treated as one, and the copy it
    /// replaced is destroyed. Everything is put back by <see cref="RestoreAll"/>.
    ///
    /// Per frame: nothing. Per change of look: one pass over the worn hair's and beard's materials (two to seven),
    /// a few dozen multiplications each (<see cref="HairRules.Rule"/>); the first time a strand texture is worn in a
    /// scene it is read back from the GPU once, measured and copied (<see cref="HairFinish.Denser"/>). With
    /// "Developer diagnostics" on, CharacterQuality writes one line per change saying what each layer is made of.
    /// </summary>
    internal static class HairLook
    {
        private const string Marker = "(RealLifeOverhaul finished)";
        private const string StrandMask = "hair_strand_mask";
        private const string StrandNormal = "hair_strand_normal";
        private const float Slack = 1.001f;   // the check in the diagnostic line allows for rounding

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int BaseColorMapId = Shader.PropertyToID("_BaseColorMap");

        private sealed class Layer
        {
            public Material Original;        // the game's material this layer stands in for
            public Material Made;            // what is on the renderer in its place: ours, or the game's clone of ours
            public HairRules.Finish Rule;
            public HairRules.Cover Cover;
            public bool Strands;             // see-through: all of the finish; false = a solid layer, back-light and highlight only
            public bool Cap;                 // painted on the skull
            public bool Measured;            // its strand texture was copied and measured
            public Color Colour;
        }

        private sealed class Finished
        {
            public SkinnedMeshRenderer Renderer;
            public Layer[] Layers;   // per material slot; null where the game's material was left alone
        }

        public static IModLogger Logger;

        private static readonly List<Finished> All = new List<Finished>(8);
        private static readonly List<Material> Scratch = new List<Material>(8);
        private static readonly List<SkinnedMeshRenderer> RendererScratch = new List<SkinnedMeshRenderer>(64);
        private static readonly Dictionary<int, Texture2D> Denser = new Dictionary<int, Texture2D>(8);
        private static readonly Dictionary<int, HairRules.Cover> Covers = new Dictionary<int, HairRules.Cover>(8);

        private static Material _template;
        private static bool _templateWarned;
        private static bool _mapsLogged;
        private static int _layers;

        /// <summary>
        /// Finishes whatever hair and beard this character is wearing now. Safe to call again and again.
        /// True if a layer was finished or took another colour: the caller may then want <see cref="Describe(AppearanceSetter)"/>.
        /// </summary>
        public static bool Apply(AppearanceSetter character)
        {
            Material template = Template(character);
            if (template == null)
            {
                return false;
            }
            Texture mask = ModAssets.Texture(StrandMask), normal = ModAssets.Texture(StrandNormal);
            if (!_mapsLogged)
            {
                _mapsLogged = true;
                Logger?.Info("Hair: finish on, keyword set borrowed from the game's '" + template.name + "', strand maps "
                             + (mask != null && normal != null ? "from the bundle" : "MISSING (the finish goes without them)") + ".");
            }
            IReadOnlyList<SkinnedMeshRenderer> renderers = character.GetSelectedMeshRenderers();
            bool changed = false;
            for (int i = 0; i < renderers.Count; i++)
            {
                if (renderers[i] != null)
                {
                    changed |= Finish(renderers[i], template, mask, normal);
                }
            }
            return changed;
        }

        // True if a layer was finished or took another colour.
        private static bool Finish(SkinnedMeshRenderer renderer, Material template, Texture mask, Texture normal)
        {
            renderer.GetSharedMaterials(Scratch);
            Finished known = Find(renderer);
            HairFinish.Settings settings = HairFinish.Settings.Default;
            bool swapped = false, changed = false;
            for (int i = 0; i < Scratch.Count; i++)
            {
                Material material = Scratch[i];
                if (material == null)
                {
                    continue;
                }
                Layer layer = known != null && i < known.Layers.Length ? known.Layers[i] : null;
                if (IsFinished(material))
                {
                    if (layer == null || layer.Original == null)
                    {
                        continue;
                    }
                    if (layer.Made != material)
                    {
                        // The game's clone of ours (it carries the new hair colour): the copy it replaced is nobody's now.
                        if (layer.Made != null)
                        {
                            UnityEngine.Object.Destroy(layer.Made);
                        }
                        layer.Made = material;
                    }
                    Color colour = material.GetColor(BaseColorId);
                    if (colour != layer.Colour)
                    {
                        layer.Rule = HairFinish.Colour(material, layer.Original, colour, mask != null && layer.Strands, settings);
                        layer.Colour = colour;
                        changed = true;
                    }
                    continue;
                }
                Material finished = HairFinish.Make(material, template, mask, normal, settings);
                if (finished == null)
                {
                    continue;   // not the hair shader (a hat): left as it is
                }
                if (known == null)
                {
                    known = new Finished { Renderer = renderer, Layers = new Layer[Scratch.Count] };
                    All.Add(known);
                }
                else if (known.Layers.Length != Scratch.Count)
                {
                    Array.Resize(ref known.Layers, Scratch.Count);
                }
                if (layer != null && layer.Made != null)
                {
                    UnityEngine.Object.Destroy(layer.Made);   // the game put a material of its own back in this slot
                }
                layer = new Layer
                {
                    Original = material, Made = finished, Strands = material.IsKeywordEnabled(HairFinish.TransparentKeyword),
                    Cap = HairFinish.IsCap(material), Colour = finished.GetColor(BaseColorId)
                };
                Texture strands = layer.Strands ? material.GetTexture(BaseColorMapId) : null;
                if (strands != null && settings.AlphaGain > 0f)
                {
                    finished.SetTexture(BaseColorMapId, DenserCopy(strands, settings, out layer.Cover));
                    layer.Measured = true;
                }
                layer.Rule = HairFinish.Colour(finished, material, layer.Colour, mask != null && layer.Strands, settings);
                known.Layers[i] = layer;
                Scratch[i] = finished;
                _layers++;
                swapped = true;
            }
            if (swapped)
            {
                renderer.sharedMaterials = Scratch.ToArray();
            }
            Scratch.Clear();
            return swapped || changed;
        }

        private static bool IsFinished(Material material)
        {
            return material.name.IndexOf(Marker, StringComparison.Ordinal) >= 0;
        }

        private static Finished Find(SkinnedMeshRenderer renderer)
        {
            for (int i = 0; i < All.Count; i++)
            {
                if (All[i].Renderer == renderer)
                {
                    return All[i];
                }
            }
            return null;
        }

        // One denser copy per strand texture, shared by every layer and character that uses it, with what it measured.
        private static Texture DenserCopy(Texture strands, HairFinish.Settings settings, out HairRules.Cover cover)
        {
            int id = strands.GetInstanceID();
            if (!Denser.TryGetValue(id, out Texture2D copy) || copy == null || !Covers.TryGetValue(id, out cover))
            {
                copy = HairFinish.Denser(strands, settings, out cover);
                Denser[id] = copy;
                Covers[id] = cover;
            }
            return copy;
        }

        /// <summary>
        /// An alpha-tested, see-through hair material of the game's, found once per scene among everything the
        /// character can wear (the female styles are made this way). Only its keyword set is used.
        /// </summary>
        private static Material Template(AppearanceSetter character)
        {
            if (_template != null)
            {
                return _template;
            }
            character.GetComponentsInChildren(true, RendererScratch);
            for (int i = 0; i < RendererScratch.Count && _template == null; i++)
            {
                RendererScratch[i].GetSharedMaterials(Scratch);
                for (int k = 0; k < Scratch.Count; k++)
                {
                    if (Scratch[k] != null && !IsFinished(Scratch[k]) && HairFinish.IsTemplate(Scratch[k]))
                    {
                        _template = Scratch[k];
                        break;
                    }
                }
            }
            RendererScratch.Clear();
            Scratch.Clear();
            if (_template == null && !_templateWarned)
            {
                _templateWarned = true;
                Logger?.Warn("Hair: no alpha-tested hair material was found on '" + character.name + "'; hair keeps the game's materials. (Logged once.)");
            }
            return _template;
        }

        // ---------------------------------------------------------------- undo

        /// <summary>Puts the game's materials back, carrying over the hair colour the game last wrote.</summary>
        public static void RestoreAll()
        {
            for (int i = 0; i < All.Count; i++)
            {
                try
                {
                    Restore(All[i]);
                }
                catch (Exception e)
                {
                    Logger?.Error(e);
                }
            }
            All.Clear();
        }

        private static void Restore(Finished finished)
        {
            if (finished.Renderer == null)
            {
                Drop(finished);   // the character is gone; its materials are not
                return;
            }
            Material[] materials = finished.Renderer.sharedMaterials;
            for (int i = 0; i < materials.Length && i < finished.Layers.Length; i++)
            {
                Layer layer = finished.Layers[i];
                Material current = materials[i];
                if (layer == null || layer.Original == null || current == null || !IsFinished(current))
                {
                    continue;
                }
                Color colour = current.GetColor(BaseColorId);
                colour.a = layer.Original.GetColor(BaseColorId).a;
                layer.Original.SetColor(BaseColorId, colour);
                materials[i] = layer.Original;
                UnityEngine.Object.Destroy(current);
            }
            finished.Renderer.sharedMaterials = materials;
            Drop(finished);
        }

        // Destroys the finished copies that are on no renderer any more (those on one were destroyed as they came off).
        private static void Drop(Finished finished)
        {
            for (int i = 0; i < finished.Layers.Length; i++)
            {
                Layer layer = finished.Layers[i];
                if (layer != null && layer.Made != null)
                {
                    UnityEngine.Object.Destroy(layer.Made);   // destroying twice in one frame is harmless
                    layer.Made = null;
                }
            }
        }

        /// <summary>Drops records of characters that no longer exist (the player's double after it is taken down).</summary>
        public static void Prune()
        {
            for (int i = All.Count - 1; i >= 0; i--)
            {
                if (All[i].Renderer == null)
                {
                    Drop(All[i]);
                    All.RemoveAt(i);
                }
            }
        }

        /// <summary>End of the scene: undo everything, free the texture copies, forget the template.</summary>
        public static void Shutdown()
        {
            RestoreAll();
            foreach (Texture2D copy in Denser.Values)
            {
                if (copy != null)
                {
                    UnityEngine.Object.Destroy(copy);
                }
            }
            Denser.Clear();
            Covers.Clear();
            _template = null;
            _templateWarned = false;
            _mapsLogged = false;
            _layers = 0;
        }

        // ---------------------------------------------------------------- saying what it did

        /// <summary>One line for the F9 report.</summary>
        public static string Describe()
        {
            return "hair finish: " + All.Count + " renderers, " + _layers + " layers finished this scene, " + Denser.Count
                   + " strand textures copied, template '" + (_template != null ? _template.name : "none") + "'";
        }

        /// <summary>
        /// How many layers of this character's hair and beard carry the finish now, of how many <paramref name="layers"/>
        /// on the hair shader it wears; <paramref name="style"/> is the first piece that has any (the hairstyle, if one is worn).
        /// </summary>
        public static int FinishedLayers(AppearanceSetter character, out int layers, out string style)
        {
            int count = 0;
            layers = 0;
            style = null;
            if (character == null)
            {
                return 0;
            }
            IReadOnlyList<SkinnedMeshRenderer> renderers = character.GetSelectedMeshRenderers();
            for (int i = 0; i < renderers.Count; i++)
            {
                if (renderers[i] == null)
                {
                    continue;
                }
                Finished known = Find(renderers[i]);
                renderers[i].GetSharedMaterials(Scratch);
                for (int k = 0; k < Scratch.Count; k++)
                {
                    if (!HairFinish.IsHair(Scratch[k]))
                    {
                        continue;
                    }
                    layers++;
                    style = style ?? renderers[i].name;
                    count += known != null && k < known.Layers.Length && known.Layers[k] != null && IsFinished(Scratch[k]) ? 1 : 0;
                }
                Scratch.Clear();
            }
            return count;
        }

        /// <summary>
        /// What this character's hair and beard are made of, in one line: per renderer the style, per layer the
        /// material, its queue, whether it is finished, what its strand texture is made of and at which gain, and the
        /// back-light and highlight it was given with the most each can send back per unit of light; then the scalp
        /// underlay, and the check against lit skin. Built only when asked for (a change of look with diagnostics on,
        /// or the test entry): never per frame.
        /// </summary>
        public static string Describe(AppearanceSetter character)
        {
            var text = new StringBuilder(640);
            text.Append("Hair check ('").Append(character != null ? character.name : "nobody").Append("'): ");
            if (character == null)
            {
                return text.Append("no character.").ToString();
            }
            int hairLayers = 0, finishedLayers = 0;
            string reason = null;
            IReadOnlyList<SkinnedMeshRenderer> renderers = character.GetSelectedMeshRenderers();
            for (int i = 0; i < renderers.Count; i++)
            {
                SkinnedMeshRenderer renderer = renderers[i];
                if (renderer == null)
                {
                    continue;
                }
                renderer.GetSharedMaterials(Scratch);
                bool any = false;
                for (int k = 0; k < Scratch.Count; k++)
                {
                    any |= HairFinish.IsHair(Scratch[k]);
                }
                if (!any)
                {
                    Scratch.Clear();
                    continue;
                }
                Finished known = Find(renderer);
                text.Append("'").Append(renderer.name).Append("' (mesh '").Append(renderer.sharedMesh != null ? renderer.sharedMesh.name : "none")
                    .Append("', drawn ").Append(renderer.enabled && !renderer.forceRenderingOff && renderer.gameObject.activeInHierarchy ? "yes" : "NO").Append("): ");
                for (int k = 0; k < Scratch.Count; k++)
                {
                    Material material = Scratch[k];
                    if (!HairFinish.IsHair(material))
                    {
                        continue;
                    }
                    hairLayers++;
                    Layer layer = known != null && k < known.Layers.Length ? known.Layers[k] : null;
                    bool finished = layer != null && layer.Made != null && IsFinished(material);
                    string name = finished ? layer.Original.name : material.name;
                    text.Append("[").Append(k).Append("] '").Append(name).Append("' ")
                        .Append(HairFinish.IsCap(finished ? layer.Original : material) ? "cap on the skull" : "strands")
                        .Append(", queue ").Append(material.renderQueue).Append(", colour ").Append(Hex(material.GetColor(BaseColorId)));
                    if (!finished)
                    {
                        text.Append(", the game's own (not finished); ");
                        continue;
                    }
                    finishedLayers++;
                    text.Append(layer.Strands ? ", finished" : ", solid layer: back-light and highlight only");
                    if (layer.Measured)
                    {
                        text.Append(", core ").Append(Percent(layer.Cover.Core)).Append(" edge ").Append(Percent(layer.Cover.Edge)).Append(" empty ")
                            .Append(Percent(layer.Cover.Empty)).Append(" at gain ").Append(Number(layer.Cover.Gain, "F2"));
                    }
                    text.Append(", back-light ").Append(Hex(HairFinish.Through(layer.Rule))).Append(" rim ").Append(Number(layer.Rule.Rim, "F3"))
                        .Append(" (at most ").Append(Number(layer.Rule.ThroughPeak, "F4")).Append("), highlight ").Append(Number(layer.Rule.Specular, "F3"))
                        .Append("/").Append(Number(layer.Rule.Secondary, "F3")).Append(" gloss ").Append(Number(layer.Rule.GlossMin, "F2")).Append("-")
                        .Append(Number(layer.Rule.GlossMax, "F2")).Append(" (at most ").Append(Number(layer.Rule.SheenPeak, "F4")).Append("), front-lit ")
                        .Append(Number(layer.Rule.FrontLit, "F4")).Append("; ");

                    Color colour = layer.Colour;
                    float luma = HairRules.Luma(HairRules.ToLinear(colour.r), HairRules.ToLinear(colour.g), HairRules.ToLinear(colour.b));
                    float sheenLimit = HairRules.SheenLimit(luma, layer.Cap) * Slack;
                    float throughLimit = HairRules.ThroughLimit(luma) * Slack;
                    if (layer.Rule.ThroughPeak > throughLimit && reason == null)
                    {
                        reason = "'" + name + "' lets " + Number(layer.Rule.ThroughPeak, "F4") + " through, " + Number(throughLimit, "F4") + " allowed";
                    }
                    if (layer.Rule.SheenPeak > sheenLimit && reason == null)
                    {
                        reason = "'" + name + "' shines " + Number(layer.Rule.SheenPeak, "F4") + ", " + Number(sheenLimit, "F4") + " allowed";
                    }
                }
                Scratch.Clear();
            }
            if (hairLayers == 0)
            {
                text.Append("no hair or beard is worn; ");
            }
            text.Append("underlay: ").Append(ModLooks.DescribeUnderlay(character)).Append("; ");
            text.Append(finishedLayers).Append(" of ").Append(hairLayers).Append(" layer(s) finished; lit skin sends back ")
                .Append(Number(HairRules.SkinLit, "F4")).Append(" per unit of light, dark hair may send back at most ")
                .Append(Number(HairRules.DarkThrough * HairRules.SkinLit, "F4")).Append(" with a lamp behind it, black hair ")
                .Append(Number(HairRules.DarkSheen * HairRules.SkinLit, "F4")).Append(" in a highlight: ");
            if (finishedLayers == 0)
            {
                text.Append(hairLayers == 0 ? "nothing to check." : "NOT CHECKED, the finish is off (the game's own hair).");
            }
            else
            {
                text.Append(reason == null ? "PASS." : "FAIL, " + reason + ".");
            }
            return text.ToString();
        }

        private static string Hex(Color colour)
        {
            return ColorUtility.ToHtmlStringRGB(colour);
        }

        private static string Percent(float share)
        {
            return (share * 100f).ToString("F1", CultureInfo.InvariantCulture) + "%";
        }

        private static string Number(float value, string format)
        {
            return value.ToString(format, CultureInfo.InvariantCulture);
        }
    }
}
