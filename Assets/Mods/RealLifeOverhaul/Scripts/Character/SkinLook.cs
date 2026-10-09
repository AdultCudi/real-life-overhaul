using System;
using System.Collections.Generic;
using System.Text;
using BAModAPI;
using BigAmbitions.Characters.Appearance;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// Puts the skin finish (<see cref="SkinFinish"/>: a fine relief in the normal map, a calmer and broken-up
    /// gloss in the mask map) on the skin of the detailed characters that show the player.
    ///
    /// The game already gives every character its own copy of each skin material (it writes the skin tone to it),
    /// so nothing is swapped: the finished maps are set on those copies, and only on those. A material that is not
    /// such a copy, or not on one of the two skin shaders, is left as it is. Work is done only when a character's
    /// look changes; the game resets the head's material on every such change, and the maps are simply set again.
    /// Putting things back never needs a record: a map of ours on a material is replaced by the game texture it was
    /// made from, wherever it is found, and anything else is left alone.
    /// </summary>
    internal static class SkinLook
    {
        private const string FaceShader = "Shader Graphs/SH_CharacterAgingHead";

        private static readonly int ToneId = Shader.PropertyToID("_MaskColorRed");

        public static IModLogger Logger;

        private static readonly SkinFinish.Maps Maps = new SkinFinish.Maps();
        private static readonly List<AppearanceSetter> Characters = new List<AppearanceSetter>(3);
        private static readonly HashSet<int> Finished = new HashSet<int>();     // materials finished in the pass that is running
        private static readonly HashSet<int> Used = new HashSet<int>();
        private static readonly List<Material> Scratch = new List<Material>(8);
        private static readonly List<SkinnedMeshRenderer> Renderers = new List<SkinnedMeshRenderer>(128);

        private static bool _logged;
        private static bool _warned;
        private static int _leftAlone;

        /// <summary>
        /// Finishes the skin this character shows now, or (<paramref name="on"/> false) gives it the game's maps back.
        /// Safe to call again and again.
        /// </summary>
        public static void Apply(AppearanceSetter character, bool on)
        {
            if (character == null)
            {
                return;
            }
            if (!on)
            {
                if (Characters.Remove(character))
                {
                    Restore(character);
                    Sweep();
                }
                return;
            }
            if (!Characters.Contains(character))
            {
                Characters.Add(character);
            }
            SkinFinish.Settings settings = SkinFinish.Settings.Default;
            Finished.Clear();
            _leftAlone = 0;

            // The face: the first material of the head (the others are the eyes and the scalp).
            SkinnedMeshRenderer head = character.HeadMesh;
            if (head != null)
            {
                head.GetSharedMaterials(Scratch);
                if (Scratch.Count > 0)
                {
                    Finish(Scratch[0], true, settings, character);
                }
                Scratch.Clear();
            }
            // The body: the skin material the game names on each piece that is worn.
            IReadOnlyList<SkinnedMeshRenderer> worn = character.GetSelectedMeshRenderers();
            for (int i = 0; i < worn.Count; i++)
            {
                SkinnedMeshRenderer renderer = worn[i];
                AppearanceElementVariant variant = renderer != null ? renderer.GetComponent<AppearanceElementVariant>() : null;
                if (variant == null || variant.skinMaterialIndex < 0)
                {
                    continue;
                }
                renderer.GetSharedMaterials(Scratch);
                if (variant.skinMaterialIndex < Scratch.Count)
                {
                    Finish(Scratch[variant.skinMaterialIndex], false, settings, character);
                }
                Scratch.Clear();
            }
            // Pieces no longer worn go back to the game's maps, so no copy is kept alive for them.
            Restore(character, Finished);
            Finished.Clear();
            Sweep();

            if (!_logged)
            {
                _logged = true;
                Logger?.Info("Skin: finish on (" + Maps.Count + " maps, " + (Maps.Bytes / 1048576f).ToString("F1") + " MB, made in " + Maps.Milliseconds.ToString("F0") + " ms).");
            }
        }

        private static void Finish(Material material, bool face, SkinFinish.Settings settings, AppearanceSetter character)
        {
            if (material == null)
            {
                return;
            }
            string shader = material.shader != null ? material.shader.name : "";
            // Runtime-made objects have negative ids: the game's per-character copy. An asset would be shared with every other character.
            bool own = material.GetInstanceID() < 0;
            if (!own || (shader != CharacterTools.StandardShader && shader != FaceShader) || !material.HasProperty(ToneId))
            {
                _leftAlone++;
                if (!_warned)
                {
                    _warned = true;
                    Logger?.Warn("Skin: material '" + material.name + "' on '" + character.name + "' (shader '" + shader + "', "
                                 + (own ? "the character's own copy" : "shared with other characters") + ") is not what the skin finish was written for; it keeps the game's maps. (Logged once.)");
                }
                return;
            }
            SkinFinish.Treat(material, face, material.GetColor(ToneId), settings, Maps);
            Finished.Add(material.GetInstanceID());
        }

        // Every material on the character that carries a map of ours and is not in <keep> gets the game's map back.
        private static void Restore(AppearanceSetter character, HashSet<int> keep = null)
        {
            if (character == null)
            {
                return;
            }
            character.GetComponentsInChildren(true, Renderers);
            for (int i = 0; i < Renderers.Count; i++)
            {
                Renderers[i].GetSharedMaterials(Scratch);
                for (int k = 0; k < Scratch.Count; k++)
                {
                    if (Scratch[k] != null && (keep == null || !keep.Contains(Scratch[k].GetInstanceID())))
                    {
                        SkinFinish.Restore(Scratch[k], Maps);
                    }
                }
            }
            Renderers.Clear();
            Scratch.Clear();
        }

        // Frees the copies nothing on the characters we look after uses any more (another skin tone, the other sex).
        private static void Sweep()
        {
            if (Maps.Count == 0)
            {
                return;
            }
            Used.Clear();
            Count(Used);
            Maps.Sweep(Used);
            Used.Clear();
        }

        private static int Count(HashSet<int> used)
        {
            int maps = 0;
            for (int c = Characters.Count - 1; c >= 0; c--)
            {
                if (Characters[c] == null)
                {
                    Characters.RemoveAt(c);
                    continue;
                }
                Characters[c].GetComponentsInChildren(true, Renderers);
                for (int i = 0; i < Renderers.Count; i++)
                {
                    Renderers[i].GetSharedMaterials(Scratch);
                    for (int k = 0; k < Scratch.Count; k++)
                    {
                        maps += SkinFinish.Collect(Scratch[k], Maps, used);
                    }
                }
                Renderers.Clear();
                Scratch.Clear();
            }
            return maps;
        }

        // ---------------------------------------------------------------- undo

        /// <summary>The game's maps back on every character; the copies are freed.</summary>
        public static void RestoreAll()
        {
            for (int i = 0; i < Characters.Count; i++)
            {
                try
                {
                    Restore(Characters[i]);
                }
                catch (Exception e)
                {
                    Logger?.Error(e);
                }
            }
            Characters.Clear();
            Maps.Clear();
        }

        /// <summary>Drops characters that no longer exist (the player's double after it is taken down).</summary>
        public static void Prune()
        {
            for (int i = Characters.Count - 1; i >= 0; i--)
            {
                if (Characters[i] == null)
                {
                    Characters.RemoveAt(i);
                }
            }
        }

        /// <summary>End of the scene: undo everything and forget what was logged.</summary>
        public static void Shutdown()
        {
            RestoreAll();
            _logged = false;
            _warned = false;
            _leftAlone = 0;
        }

        /// <summary>The block for the F9 report.</summary>
        public static void Describe(StringBuilder text)
        {
            int inUse = Count(null);
            text.AppendLine("skin finish: option " + Settings.CharacterSkin + ", characters " + Characters.Count + ", maps on their materials " + inUse
                            + ", copies held " + Maps.Count + " (" + (Maps.Bytes / 1048576f).ToString("F2") + " MB), made this scene " + Maps.MadeEver
                            + " in " + Maps.Milliseconds.ToString("F0") + " ms, materials left alone at the last change of look " + _leftAlone);
            for (int c = 0; c < Characters.Count; c++)
            {
                AppearanceSetter character = Characters[c];
                if (character == null)
                {
                    continue;
                }
                character.GetComponentsInChildren(false, Renderers);
                for (int i = 0; i < Renderers.Count; i++)
                {
                    Renderers[i].GetSharedMaterials(Scratch);
                    for (int k = 0; k < Scratch.Count; k++)
                    {
                        Material material = Scratch[k];
                        if (material == null || SkinFinish.Collect(material, Maps, null) == 0)
                        {
                            continue;
                        }
                        Color tone = material.HasProperty(ToneId) ? material.GetColor(ToneId) : Color.white;
                        text.AppendLine("  on '" + character.name + "' / '" + Renderers[i].name + "': '" + material.name + "' tone " + ColorUtility.ToHtmlStringRGB(tone)
                                        + " darkness " + SkinFinish.Darkness(tone).ToString("0.##") + MapList(material));
                    }
                }
                Renderers.Clear();
                Scratch.Clear();
            }
        }

        private static readonly string[] MapNames = { "_MaskMap", "_NormalMap", "_MaskMap_1", "_NormalMap_1" };

        private static string MapList(Material material)
        {
            var text = new StringBuilder();
            for (int i = 0; i < MapNames.Length; i++)
            {
                Texture texture = material.HasProperty(MapNames[i]) ? material.GetTexture(MapNames[i]) : null;
                if (texture != null && Maps.IsCopy(texture))
                {
                    text.Append("  ").Append(MapNames[i]).Append(" ").Append(texture.width).Append("x").Append(texture.height).Append(" ").Append(texture.graphicsFormat);
                }
            }
            return text.ToString();
        }
    }
}
