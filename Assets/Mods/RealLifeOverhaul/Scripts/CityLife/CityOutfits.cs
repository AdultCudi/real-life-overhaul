using System.Collections.Generic;
using BigAmbitions.Characters.Appearance;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>What somebody in the living city wears. Civilians are dressed at random; the rest wear work clothes.</summary>
    public enum CityLook { Civilian, RoadWorker, Sweeper, Cleaner, Vendor, Canvasser, Courier, Postie, Painter, Gardener, Doorman }

    /// <summary>
    /// Work clothes for the living city, made as People/Uniforms makes the officers': the game has no work wear, so each
    /// outfit is a set of the game's garments found by name and recoloured through the colour entry the game reads when it
    /// builds the look. Each recolouring is recorded and put back before the character is dressed as anyone else; a garment
    /// the game lacks is logged once and that slot keeps the rolled clothes.
    /// </summary>
    internal static class CityOutfits
    {
        /// <summary>One colour entry as it was before an outfit recoloured it.</summary>
        internal struct Dye
        {
            public AppearanceElementColor Entry;
            public Color Base, R, G, B, A;
            public Color SheenR, SheenG, SheenB, SheenA;

            public static Dye Of(AppearanceElementColor entry) => new Dye
            {
                Entry = entry, Base = entry.baseColor, R = entry.r, G = entry.g, B = entry.b, A = entry.a,
                SheenR = entry.specularR, SheenG = entry.specularG, SheenB = entry.specularB, SheenA = entry.specularA
            };
        }

        private readonly struct Garment
        {
            public readonly AppearanceElementType Slot;
            public readonly string Name;
            public readonly Color Main;     // base colour and the first channel the garment has
            public readonly Color Second;   // the next channel
            public readonly Color Trim;     // any further channels
            public readonly bool Dyed;

            public Garment(AppearanceElementType slot, string name, Color main, Color second, Color trim)
            {
                Slot = slot;
                Name = name;
                Main = main;
                Second = second;
                Trim = trim;
                Dyed = true;
            }

            public Garment(AppearanceElementType slot, string name)
            {
                Slot = slot;
                Name = name;
                Main = Second = Trim = default;
                Dyed = false;
            }
        }

        private const string Nothing = "None";   // the empty choice every accessory slot has

        private static readonly Color HiVisOrange = new Color(0.95f, 0.38f, 0.04f);
        private static readonly Color HiVisYellow = new Color(0.88f, 0.86f, 0.10f);
        private static readonly Color WorkNavy = new Color(0.07f, 0.09f, 0.16f);
        private static readonly Color BootTan = new Color(0.36f, 0.25f, 0.14f);
        private static readonly Color Black = new Color(0.035f, 0.035f, 0.040f);
        private static readonly Color CouncilGreen = new Color(0.10f, 0.33f, 0.20f);
        private static readonly Color CouncilDark = new Color(0.07f, 0.16f, 0.12f);
        private static readonly Color CleanerBlue = new Color(0.38f, 0.56f, 0.74f);
        private static readonly Color Charcoal = new Color(0.10f, 0.10f, 0.11f);
        private static readonly Color VendorWhite = new Color(0.90f, 0.90f, 0.87f);
        private static readonly Color VendorRed = new Color(0.62f, 0.10f, 0.08f);
        private static readonly Color CanvasserRed = new Color(0.80f, 0.12f, 0.10f);

        private static readonly Garment[] Bare =
        {
            new Garment(AppearanceElementType.HairAccessory, Nothing),
            new Garment(AppearanceElementType.TorsoAccessory, Nothing),
            new Garment(AppearanceElementType.LegsAccessory, Nothing),
            new Garment(AppearanceElementType.FeetAccessory, Nothing)
        };

        private static readonly Garment[] RoadWorker =
        {
            new Garment(AppearanceElementType.Torso, "UniformShirt", HiVisOrange, HiVisOrange, HiVisYellow),
            new Garment(AppearanceElementType.Legs, "CargoPantsLong", WorkNavy, WorkNavy, HiVisYellow),
            new Garment(AppearanceElementType.Feet, "Boots", BootTan, Black, BootTan),
            new Garment(AppearanceElementType.HeadAccessory, "Hat", HiVisYellow, HiVisYellow, HiVisYellow)
        };

        private static readonly Garment[] Sweeper =
        {
            new Garment(AppearanceElementType.Torso, "UniformShirt", CouncilGreen, CouncilGreen, HiVisYellow),
            new Garment(AppearanceElementType.Legs, "CargoPantsLong", CouncilDark, CouncilDark, CouncilDark),
            new Garment(AppearanceElementType.Feet, "Boots", Black, Black, Black),
            new Garment(AppearanceElementType.HeadAccessory, Nothing)
        };

        private static readonly Garment[] Cleaner =
        {
            new Garment(AppearanceElementType.Torso, "UniformShirt", CleanerBlue, CleanerBlue, Charcoal),
            new Garment(AppearanceElementType.Legs, "CargoPantsLong", Charcoal, Charcoal, Charcoal),
            new Garment(AppearanceElementType.Feet, "Boots", Black, Black, Black),
            new Garment(AppearanceElementType.HeadAccessory, Nothing)
        };

        private static readonly Garment[] Vendor =
        {
            new Garment(AppearanceElementType.Torso, "UniformShirt", VendorWhite, VendorWhite, VendorRed),
            new Garment(AppearanceElementType.Legs, "SuitPants", Charcoal, Charcoal, Charcoal),
            new Garment(AppearanceElementType.HeadAccessory, "Hat", VendorRed, VendorRed, VendorWhite)
        };

        private static readonly Garment[] Canvasser =
        {
            new Garment(AppearanceElementType.Torso, "UniformShirt", CanvasserRed, CanvasserRed, VendorWhite),
            new Garment(AppearanceElementType.HeadAccessory, Nothing)
        };

        private static readonly Color CourierBrown = new Color(0.30f, 0.19f, 0.11f);
        private static readonly Color CourierGold = new Color(0.80f, 0.60f, 0.12f);
        private static readonly Color PostBlue = new Color(0.16f, 0.26f, 0.50f);
        private static readonly Color PostGrey = new Color(0.30f, 0.33f, 0.38f);
        private static readonly Color PainterWhite = new Color(0.86f, 0.86f, 0.82f);
        private static readonly Color GardenGreen = new Color(0.20f, 0.38f, 0.14f);
        private static readonly Color GardenKhaki = new Color(0.45f, 0.40f, 0.27f);

        private static readonly Garment[] Courier =
        {
            new Garment(AppearanceElementType.Torso, "UniformShirt", CourierBrown, CourierBrown, CourierGold),
            new Garment(AppearanceElementType.Legs, "CargoPantsLong", CourierBrown, CourierBrown, CourierBrown),
            new Garment(AppearanceElementType.Feet, "Boots", Black, Black, Black),
            new Garment(AppearanceElementType.HeadAccessory, "Hat", CourierBrown, CourierBrown, CourierGold)
        };

        private static readonly Garment[] Postie =
        {
            new Garment(AppearanceElementType.Torso, "UniformShirt", PostBlue, PostBlue, VendorWhite),
            new Garment(AppearanceElementType.Legs, "CargoPantsLong", PostGrey, PostGrey, PostGrey),
            new Garment(AppearanceElementType.Feet, "Boots", Black, Black, Black),
            new Garment(AppearanceElementType.HeadAccessory, "Hat", PostBlue, PostBlue, VendorWhite)
        };

        private static readonly Garment[] Painter =
        {
            new Garment(AppearanceElementType.Torso, "UniformShirt", PainterWhite, PainterWhite, PainterWhite),
            new Garment(AppearanceElementType.Legs, "CargoPantsLong", PainterWhite, PainterWhite, PainterWhite),
            new Garment(AppearanceElementType.Feet, "Boots", BootTan, Black, BootTan),
            new Garment(AppearanceElementType.HeadAccessory, "Hat", PainterWhite, PainterWhite, PainterWhite)
        };

        private static readonly Garment[] Gardener =
        {
            new Garment(AppearanceElementType.Torso, "UniformShirt", GardenGreen, GardenGreen, GardenKhaki),
            new Garment(AppearanceElementType.Legs, "CargoPantsLong", GardenKhaki, GardenKhaki, GardenKhaki),
            new Garment(AppearanceElementType.Feet, "Boots", BootTan, Black, BootTan),
            new Garment(AppearanceElementType.HeadAccessory, Nothing)
        };

        private static readonly Garment[] Doorman =
        {
            new Garment(AppearanceElementType.Torso, "UniformShirt", Black, Black, Charcoal),
            new Garment(AppearanceElementType.Legs, "SuitPants", Black, Black, Black),
            new Garment(AppearanceElementType.HeadAccessory, Nothing)
        };

        private static readonly HashSet<string> Missing = new HashSet<string>();

        /// <summary>
        /// Puts the outfit on a character whose look is already rolled, recording what it recolours. The caller rebuilds the
        /// visuals.
        /// </summary>
        public static void Wear(AppearanceSetter appearance, CityLook look, List<Dye> dyes)
        {
            Wear(appearance, look, dyes, appearance.data.elements);
        }

        /// <summary>
        /// The same outfit written into the caller's own list instead of the character's data, for work clothes worn over
        /// somebody's own for a while (Jobs/JobUniform): shown with AppearanceSetter.UpdateVisuals(list), gone with
        /// UpdateVisuals(). The character's own colour entries are recoloured and recorded in <paramref name="dyes"/>.
        /// </summary>
        public static void Wear(AppearanceSetter appearance, CityLook look, List<Dye> dyes, List<AppearanceElementData> elements)
        {
            Garment[] outfit = look == CityLook.RoadWorker ? RoadWorker : look == CityLook.Sweeper ? Sweeper : look == CityLook.Cleaner ? Cleaner
                : look == CityLook.Vendor ? Vendor : look == CityLook.Canvasser ? Canvasser : look == CityLook.Courier ? Courier : look == CityLook.Postie ? Postie
                : look == CityLook.Painter ? Painter : look == CityLook.Gardener ? Gardener : look == CityLook.Doorman ? Doorman : null;
            if (outfit == null)
            {
                return;
            }
            for (int i = 0; i < outfit.Length; i++)
            {
                Put(appearance, outfit[i], dyes, elements);
            }
            for (int i = 0; i < Bare.Length; i++)
            {
                Put(appearance, Bare[i], dyes, elements);
            }
        }

        /// <summary>Puts back every colour an outfit changed on this character.</summary>
        public static void Restore(List<Dye> dyes)
        {
            for (int i = dyes.Count - 1; i >= 0; i--)
            {
                Dye dye = dyes[i];
                if (dye.Entry != null)
                {
                    dye.Entry.baseColor = dye.Base;
                    dye.Entry.r = dye.R;
                    dye.Entry.g = dye.G;
                    dye.Entry.b = dye.B;
                    dye.Entry.a = dye.A;
                    dye.Entry.specularR = dye.SheenR;
                    dye.Entry.specularG = dye.SheenG;
                    dye.Entry.specularB = dye.SheenB;
                    dye.Entry.specularA = dye.SheenA;
                }
            }
            dyes.Clear();
        }

        private static void Put(AppearanceSetter appearance, Garment garment, List<Dye> dyes, List<AppearanceElementData> elements)
        {
            AppearanceElementColor entry = Choose(appearance, garment.Slot, garment.Name, garment.Dyed, elements, out bool missing);
            if (missing && Missing.Add(garment.Slot + "/" + garment.Name))
            {
                CityLifeDirector.Logger?.Warn($"City life: the game has no '{garment.Name}' for {garment.Slot}; work clothes keep whatever was picked there. (Logged once.)");
            }
            if (entry == null)
            {
                return;
            }
            dyes.Add(Dye.Of(entry));
            Recolour(entry, garment.Main, garment.Second, garment.Trim);
        }

        /// <summary>
        /// Puts the garment of that name in its slot and returns its first plain colour entry, chosen, for the caller to
        /// recolour; null when it is not to be dyed or has only patterned colours. <paramref name="missing"/>: the game
        /// has no garment of that name, and nothing was changed.
        /// </summary>
        internal static AppearanceElementColor Choose(AppearanceSetter appearance, AppearanceElementType slot, string name, bool dyed,
            List<AppearanceElementData> elements, out bool missing)
        {
            missing = false;
            List<AppearanceElementVariant> variants = appearance.GetElementVariants(slot);
            if (variants == null || variants.Count == 0)
            {
                return null;   // this body has no such slot
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
                missing = true;
                return null;
            }

            AppearanceElementData element = null;
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
            if (!dyed || variant.colors == null)
            {
                return null;
            }

            // A patterned (textured) colour cannot be recoloured.
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
                return null;
            }
            element.colorId = entry.id;
            return entry;
        }

        /// <summary>Main goes on the base colour and the first channel in use, second on the next, trim on the rest.</summary>
        internal static void Recolour(AppearanceElementColor entry, Color main, Color second, Color trim)
        {
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

        // The garment's sheen shows at glancing angles, so it follows the dye.
        private static Color Sheen(Color dye)
        {
            Color sheen = Color.Lerp(dye, Color.white, 0.12f);
            sheen.a = 0f;
            return sheen;
        }

        private static Color Shade(int channel, Color main, Color second, Color trim)
        {
            return channel == 0 ? main : channel == 1 ? second : trim;
        }
    }
}
