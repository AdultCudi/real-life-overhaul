using System.Collections.Generic;
using BigAmbitions.Characters.Appearance;
using UnityEngine;

namespace RealLifeOverhaul.Events
{
    /// <summary>
    /// Turnout gear for the fire crew, made the way the living city's work clothes are
    /// (CityLife/CityOutfits): the game's own garments by name, recoloured through the colour
    /// entry the game reads when it builds the look: a black jacket and trousers with yellow
    /// trim, black boots, nothing on the head (the helmet is a prop). What is recoloured is
    /// recorded on the person, where their pool puts it back before dressing them as anyone
    /// else. Somebody still in the gear from the last fire is not dressed again: no mesh merge.
    /// </summary>
    internal static class FireOutfit
    {
        private static readonly Color Turnout = new Color(0.055f, 0.06f, 0.075f);
        private static readonly Color Trim = new Color(0.88f, 0.80f, 0.10f);
        private static readonly Color Boot = new Color(0.03f, 0.03f, 0.035f);
        private const string Nothing = "None";

        private static readonly HashSet<CityPerson> Dressed = new HashSet<CityPerson>();
        private static readonly HashSet<string> Missing = new HashSet<string>();
        private static readonly Dictionary<AppearanceElementColor, CityOutfits.Dye> Originals = new Dictionary<AppearanceElementColor, CityOutfits.Dye>(4);

        public static bool Wearing(CityPerson person)
        {
            return Dressed.Contains(person);
        }

        public static void Forget(CityPerson person)
        {
            Dressed.Remove(person);
        }

        public static void Reset()
        {
            Dressed.Clear();
        }

        /// <summary>Dresses a person from the event pool as a firefighter. One mesh merge, and none if they still wear it.</summary>
        public static void Wear(CityPerson person)
        {
            if (person == null || person.Lost || !Dressed.Add(person))
            {
                return;
            }
            AppearanceSetter appearance = person.Character.appearanceSetter;
            Put(appearance, AppearanceElementType.Torso, "UniformShirt", Turnout, Turnout, Trim, person.Dyes);
            Put(appearance, AppearanceElementType.Legs, "CargoPantsLong", Turnout, Turnout, Trim, person.Dyes);
            Put(appearance, AppearanceElementType.Feet, "Boots", Boot, Boot, Boot, person.Dyes);
            Put(appearance, AppearanceElementType.HeadAccessory, Nothing, default, default, default, null);
            Put(appearance, AppearanceElementType.HairAccessory, Nothing, default, default, default, null);
            Put(appearance, AppearanceElementType.TorsoAccessory, Nothing, default, default, default, null);
            Put(appearance, AppearanceElementType.LegsAccessory, Nothing, default, default, default, null);
            Put(appearance, AppearanceElementType.FeetAccessory, Nothing, default, default, default, null);
            Looks.Count("events: a firefighter's look being built (a mesh merge)");
            appearance.UpdateVisuals();
            Looks.Done();
        }

        // One garment by name; with a list of dyes, recoloured in its first plain colour (see CityOutfits.Put, which this follows).
        private static void Put(AppearanceSetter appearance, AppearanceElementType slot, string name, Color main, Color second, Color trim, List<CityOutfits.Dye> dyes)
        {
            List<AppearanceElementVariant> variants = appearance.GetElementVariants(slot);
            if (variants == null || variants.Count == 0)
            {
                return;
            }
            AppearanceElementVariant variant = null;
            for (int i = 0; i < variants.Count; i++)
            {
                if (variants[i].name == name)
                {
                    variant = variants[i];
                    break;
                }
            }
            if (variant == null)
            {
                if (Missing.Add(slot + "/" + name))
                {
                    EventDirector.Logger?.Warn($"Fire crew: the game has no '{name}' for {slot}; that slot keeps what was picked. (Logged once.)");
                }
                return;
            }
            AppearanceElementData element = null;
            List<AppearanceElementData> elements = appearance.data.elements;
            for (int i = 0; i < elements.Count; i++)
            {
                if (elements[i].type == slot)
                {
                    element = elements[i];
                    break;
                }
            }
            if (element == null)
            {
                element = new AppearanceElementData { type = slot };
                elements.Add(element);
            }
            element.variantId = variant.id;
            element.colorId = null;
            if (dyes == null || variant.colors == null)
            {
                return;
            }
            AppearanceElementColor entry = null;
            for (int i = 0; i < variant.colors.Length; i++)
            {
                if (!variant.colors[i].hasTexture)
                {
                    entry = variant.colors[i];
                    break;
                }
            }
            if (entry == null)
            {
                element.colorId = variant.colors.Length > 0 ? variant.colors[0].id : null;
                return;
            }
            element.colorId = entry.id;
            // The colour entry is the game's own and shared: three firefighters dye the same one. Each of them is given
            // the entry as it was before the first of them touched it, so whichever is undressed last still puts the
            // game's colours back, not another firefighter's black.
            if (!Originals.TryGetValue(entry, out CityOutfits.Dye original))
            {
                original = new CityOutfits.Dye
                {
                    Entry = entry, Base = entry.baseColor, R = entry.r, G = entry.g, B = entry.b, A = entry.a,
                    SheenR = entry.specularR, SheenG = entry.specularG, SheenB = entry.specularB, SheenA = entry.specularA
                };
                Originals[entry] = original;
            }
            dyes.Add(original);
            if (entry.hasBaseColor)
            {
                entry.baseColor = main;
            }
            int used = 0;
            if (entry.hasChannelR)
            {
                entry.r = Shade(used++, main, second, trim);
                entry.specularR = Sheen(entry.r);
            }
            if (entry.hasChannelG)
            {
                entry.g = Shade(used++, main, second, trim);
                entry.specularG = Sheen(entry.g);
            }
            if (entry.hasChannelB)
            {
                entry.b = Shade(used++, main, second, trim);
                entry.specularB = Sheen(entry.b);
            }
            if (entry.hasChannelA)
            {
                entry.a = Shade(used, main, second, trim);
                entry.specularA = Sheen(entry.a);
            }
        }

        private static Color Shade(int channel, Color main, Color second, Color trim)
        {
            return channel == 0 ? main : channel == 1 ? second : trim;
        }

        private static Color Sheen(Color dye)
        {
            Color sheen = Color.Lerp(dye, Color.white, 0.12f);
            sheen.a = 0f;
            return sheen;
        }
    }
}
