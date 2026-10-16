using System.Collections.Generic;
using BigAmbitions.Characters;
using BigAmbitions.Characters.Appearance;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// A kept look (<see cref="PersonLook"/>) to and from the game's own appearance data (CharacterData): read off a figure
    /// of the crowd (<see cref="Of"/>), and put on whoever the mod dresses from a seed that is a kept look's (<see cref="Dress"/>:
    /// Social/SocialCast, Family/FamilyCast, Home/Roommates/RoommateCast and the portrait camera's, Phone/Contacts/PortraitTurn).
    ///
    /// A character the mod dresses again and again (the social life's two, the portrait camera's one) keeps from one look to
    /// the next what the game's roll does not set again: the parts a roll of casual clothes leaves alone, the face's shapes,
    /// the eye colour. A kept look sets all of them, so they are put back as they were when the next look is a rolled one
    /// (<see cref="Dress"/> returning false): a rolled face is the same with or without a kept look before it, which the
    /// portrait caches count on.
    ///
    /// COST: on a look built or a face drawn only (each a mesh merge or a portrait render already): a few lists of twenty
    /// entries. Nothing per frame.
    /// </summary>
    internal static class PersonLookGame
    {
        private sealed class Before
        {
            public List<AppearanceElementData> Parts;
            public List<FacialBlendshape> Shapes;
            public Color32 Eyes;
        }

        private static readonly Dictionary<int, Before> Dressed = new Dictionary<int, Before>(4);   // by the appearance's instance: what a kept look took the place of
        private const int DressedMost = 16;   // characters remembered at most (the mod dresses a handful again and again); past that the oldest are let go

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
        /// The kept look of 'seed' put on the appearance (sex, build, skin, eyes, every part, the face's shapes; the caller sets
        /// the age and caps the build as for anybody it dresses, then merges the meshes); true if the seed is a kept look's.
        /// False for a rolled seed, with what an earlier kept look took the place of put back: the caller rolls the look then.
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
                    Dressed.Clear();   // (characters long gone: the mod's own are a handful)
                }
                Dressed.Add(id, new Before { Parts = data.elements, Shapes = data.blendshapes, Eyes = data.eyesColor });
            }
            Fill(data, look);
            return true;
        }

        // Everything of a kept look but the age into the game's appearance data.
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

        /// <summary>The kept look of 'seed' as appearance data of its own, of the age given (days); null for a rolled seed.</summary>
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
