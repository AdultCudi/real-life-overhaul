using System;
using System.Collections.Generic;
using BAModAPI;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// Puts the hair finish (<see cref="HairFinish"/>: solid strand cores through the depth prepass, strand maps,
    /// gloss that follows the hair colour) on the hair and beard of the detailed characters that show the player.
    ///
    /// Work is done only when a character's look changes: each layer of the worn hair is swapped for a finished
    /// copy, and when only the colour changed the copies are re-tinted. The game writes hair colours through
    /// Renderer.materials, which leaves its own clone of whatever is on the renderer; a clone of a finished layer
    /// is recognised by its name and treated as one. Everything is put back by <see cref="RestoreAll"/>.
    /// </summary>
    internal static class HairLook
    {
        private const string Marker = "(RealLifeOverhaul finished)";
        private const string StrandMask = "hair_strand_mask";
        private const string StrandNormal = "hair_strand_normal";

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int BaseColorMapId = Shader.PropertyToID("_BaseColorMap");

        private sealed class Finished
        {
            public SkinnedMeshRenderer Renderer;
            public Material[] Originals;   // per slot: the game's material a finished layer stands in for, or null
        }

        public static IModLogger Logger;

        private static readonly List<Finished> All = new List<Finished>(8);
        private static readonly List<Material> Scratch = new List<Material>(8);
        private static readonly List<SkinnedMeshRenderer> RendererScratch = new List<SkinnedMeshRenderer>(64);
        private static readonly Dictionary<int, Texture2D> Denser = new Dictionary<int, Texture2D>(8);

        private static Material _template;
        private static bool _templateWarned;
        private static bool _mapsLogged;
        private static int _layers;

        /// <summary>Finishes whatever hair and beard this character is wearing now. Safe to call again and again.</summary>
        public static void Apply(AppearanceSetter character)
        {
            Material template = Template(character);
            if (template == null)
            {
                return;
            }
            Texture mask = ModAssets.Texture(StrandMask), normal = ModAssets.Texture(StrandNormal);
            if (!_mapsLogged)
            {
                _mapsLogged = true;
                Logger?.Info("Hair: finish on, keyword set borrowed from the game's '" + template.name + "', strand maps "
                             + (mask != null && normal != null ? "from the bundle" : "MISSING (the finish goes without them)") + ".");
            }
            IReadOnlyList<SkinnedMeshRenderer> renderers = character.GetSelectedMeshRenderers();
            for (int i = 0; i < renderers.Count; i++)
            {
                if (renderers[i] != null)
                {
                    Finish(renderers[i], template, mask, normal);
                }
            }
        }

        private static void Finish(SkinnedMeshRenderer renderer, Material template, Texture mask, Texture normal)
        {
            renderer.GetSharedMaterials(Scratch);
            Finished known = Find(renderer);
            HairFinish.Settings settings = HairFinish.Settings.Default;
            bool changed = false;
            for (int i = 0; i < Scratch.Count; i++)
            {
                Material material = Scratch[i];
                if (material == null)
                {
                    continue;
                }
                if (IsFinished(material))
                {
                    // Ours, or the game's clone of ours carrying a new hair colour: take the colour, redo the gloss.
                    Material source = known != null && i < known.Originals.Length ? known.Originals[i] : null;
                    if (source != null)
                    {
                        HairFinish.Colour(material, source, material.GetColor(BaseColorId), mask != null, settings.Calm);
                    }
                    continue;
                }
                Material finished = HairFinish.Make(material, template, mask, normal, settings);
                if (finished == null)
                {
                    continue;   // not strand hair (a hat, an opaque scalp): left as it is
                }
                Texture strands = material.GetTexture(BaseColorMapId);
                if (strands != null && settings.AlphaGain > 0f)
                {
                    finished.SetTexture(BaseColorMapId, DenserCopy(strands, settings.AlphaGain));
                }
                if (known == null)
                {
                    known = new Finished { Renderer = renderer, Originals = new Material[Scratch.Count] };
                    All.Add(known);
                }
                else if (known.Originals.Length != Scratch.Count)
                {
                    Array.Resize(ref known.Originals, Scratch.Count);
                }
                known.Originals[i] = material;
                Scratch[i] = finished;
                _layers++;
                changed = true;
            }
            if (changed)
            {
                renderer.sharedMaterials = Scratch.ToArray();
            }
            Scratch.Clear();
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

        // One denser copy per strand texture, shared by every layer and character that uses it.
        private static Texture DenserCopy(Texture strands, float gain)
        {
            int id = strands.GetInstanceID();
            if (!Denser.TryGetValue(id, out Texture2D copy) || copy == null)
            {
                copy = HairFinish.Denser(strands, gain);
                Denser[id] = copy;
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
                return;   // the character is gone, and its materials with it
            }
            Material[] materials = finished.Renderer.sharedMaterials;
            for (int i = 0; i < materials.Length && i < finished.Originals.Length; i++)
            {
                Material original = finished.Originals[i], current = materials[i];
                if (original == null || current == null || !IsFinished(current))
                {
                    continue;
                }
                Color colour = current.GetColor(BaseColorId);
                colour.a = original.GetColor(BaseColorId).a;
                original.SetColor(BaseColorId, colour);
                materials[i] = original;
                UnityEngine.Object.Destroy(current);
            }
            finished.Renderer.sharedMaterials = materials;
        }

        /// <summary>Drops records of characters that no longer exist (the player's double after it is taken down).</summary>
        public static void Prune()
        {
            for (int i = All.Count - 1; i >= 0; i--)
            {
                if (All[i].Renderer == null)
                {
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
            _template = null;
            _templateWarned = false;
            _mapsLogged = false;
            _layers = 0;
        }

        /// <summary>One line for the F9 report.</summary>
        public static string Describe()
        {
            return "hair finish: " + All.Count + " renderers, " + _layers + " layers finished this scene, " + Denser.Count
                   + " strand textures copied, template '" + (_template != null ? _template.name : "none") + "'";
        }
    }
}
