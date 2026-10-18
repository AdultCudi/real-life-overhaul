using System.Collections.Generic;
using BigAmbitions.Characters;
using BigAmbitions.Characters.Appearance;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// A kept look (<see cref="PersonLook"/>) to and from the game's appearance data. What a kept look replaced is put back
    /// before a rolled one, so a rolled face never depends on what came before (the portrait caches count on that).
    /// </summary>
    internal static class PersonLookGame
    {
        private sealed class Before
        {
            public List<AppearanceElementData> Parts;
            public List<FacialBlendshape> Shapes;
            public Color32 Eyes;
        }

        private static readonly Dictionary<int, Before> Dressed = new Dictionary<int, Before>(4);   // by appearance instance: what a kept look replaced
        private const int DressedMost = 16;   // characters remembered; past that the record starts again

        /// <summary>The look of a character as the game made it, as a kept look's text; null if it has none.</summary>
        public static string Of(CharacterData data)
        {
            if (data == null || data.elements == null || data.elements.Count == 0)
            {
                return null;
            }
            var look = new PersonLook
            {
                Female = data.gender == Gender.Female, Strength = data.strength, Fatness = data.fatness, Skin = Pack(data.color), Eyes = Pack(data.eyesColor)
            };
            for (int i = 0; i < data.elements.Count; i++)
            {
                AppearanceElementData e = data.elements[i];
                if (e != null && !string.IsNullOrEmpty(e.variantId))
                {
                    look.Parts.Add(new LookPart { Type = (int)e.type, Variant = e.variantId, Colour = e.colorId });
                }
            }
            for (int i = 0; data.blendshapes != null && i < data.blendshapes.Count; i++)
            {
                FacialBlendshape s = data.blendshapes[i];
                if (s != null && !string.IsNullOrEmpty(s.name))
                {
                    look.Shapes.Add(new LookShape { Name = s.name, Value = s.value, Category = (int)s.category, Kind = (int)s.type });
                }
            }
            return look.Parts.Count > 0 ? look.Encode() : null;
        }

        /// <summary>
        /// Puts the kept look of <paramref name="seed"/> on the appearance, all but the age; the caller merges the meshes.
        /// False for a rolled seed, after putting back what an earlier kept look replaced: the caller rolls the look then.
        /// </summary>
        public static bool Dress(AppearanceSetter appearance, int seed)
        {
            CharacterData data = appearance != null ? appearance.data : null;
            if (data == null)
            {
                return false;
            }
            int id = appearance.GetInstanceID();
            if (!PersonLooks.TryGet(seed, out PersonLook look))
            {
                if (Dressed.TryGetValue(id, out Before was))
                {
                    data.elements = was.Parts ?? new List<AppearanceElementData>();
                    data.blendshapes = was.Shapes ?? new List<FacialBlendshape>();
                    data.eyesColor = was.Eyes;
                    Dressed.Remove(id);
                }
                return false;
            }
            if (!Dressed.ContainsKey(id))
            {
                if (Dressed.Count >= DressedMost)
                {
                    Dressed.Clear();   // long gone: the mod dresses only a handful
                }
                Dressed.Add(id, new Before { Parts = data.elements, Shapes = data.blendshapes, Eyes = data.eyesColor });
            }
            Fill(data, look);
            return true;
        }

        private static void Fill(CharacterData data, PersonLook look)
        {
            data.gender = look.Female ? Gender.Female : Gender.Male;
            data.strength = look.Strength;
            data.fatness = look.Fatness;
            data.color = Unpack(look.Skin);
            data.eyesColor = Unpack(look.Eyes);
            var parts = new List<AppearanceElementData>(look.Parts.Count);
            for (int i = 0; i < look.Parts.Count; i++)
            {
                parts.Add(new AppearanceElementData { type = (AppearanceElementType)look.Parts[i].Type, variantId = look.Parts[i].Variant, colorId = look.Parts[i].Colour });
            }
            data.elements = parts;
            var shapes = new List<FacialBlendshape>(look.Shapes.Count);
            for (int i = 0; i < look.Shapes.Count; i++)
            {
                shapes.Add(new FacialBlendshape
                {
                    name = look.Shapes[i].Name, value = look.Shapes[i].Value, category = (AppearanceElementType)look.Shapes[i].Category, type = (FacialBlendshape.BlendshapeType)look.Shapes[i].Kind
                });
            }
            data.blendshapes = shapes;
        }

        /// <summary>The kept look of <paramref name="seed"/> as appearance data of its own, aged in days; null for a rolled seed.</summary>
        public static CharacterData Data(int seed, int ageInDays)
        {
            if (!PersonLooks.TryGet(seed, out PersonLook look))
            {
                return null;
            }
            var data = new CharacterData { ageInDays = ageInDays };
            Fill(data, look);
            return data;
        }

        private static uint Pack(Color32 c) => ((uint)c.r << 24) | ((uint)c.g << 16) | ((uint)c.b << 8) | c.a;

        private static Color32 Unpack(uint v) => new Color32((byte)(v >> 24), (byte)(v >> 16), (byte)(v >> 8), (byte)v);
    }
}
