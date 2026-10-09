using System.Collections.Generic;
using BigAmbitions.Characters.Appearance;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The two uniforms: what every officer and every paramedic wears, the same each time.
    /// The game has no police or paramedic clothing, so each uniform is a fixed set of the
    /// game's own garments, named here, in service colours: a shirt, long trousers and boots,
    /// with every accessory slot emptied. A garment is found by its name on the character
    /// (the names are the same for men and women; the ids are not) and recoloured through the
    /// colour entry the game itself reads when it builds the look.
    /// Colour entries belong to the one character, but they outlive the outfit, so each change
    /// is recorded and put back before that character is dressed as anyone else.
    /// </summary>
    internal static class Uniforms
    {
        /// <summary>One colour entry as it was before a uniform recoloured it.</summary>
        internal struct Dye
        {
            public AppearanceElementColor Entry;
            public Color Base, R, G, B, A;
            public Color SheenR, SheenG, SheenB, SheenA;
        }

        // One garment of a uniform. The game splits a garment's colour over a base and up to four
        // mask channels; which channel is which part differs per garment, hence the three colours.
        internal readonly struct Garment
        {
            public readonly AppearanceElementType Slot;
            public readonly string Name;
            public readonly Color Main;     // base colour and the first channel the garment has
            public readonly Color Second;   // the next channel: the body of a shirt, the sole of a boot
            public readonly Color Trim;     // any further channels: collar, laces, badge
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

        private static readonly Color Navy = new Color(0.050f, 0.075f, 0.180f);
        private static readonly Color NavyDark = new Color(0.030f, 0.040f, 0.095f);
        private static readonly Color Black = new Color(0.035f, 0.035f, 0.040f);
        private static readonly Color BlackSole = new Color(0.020f, 0.020f, 0.020f);
        private static readonly Color MedicWhite = new Color(0.900f, 0.920f, 0.890f);
        private static readonly Color MedicGreen = new Color(0.040f, 0.330f, 0.190f);
        private static readonly Color MedicGreenDark = new Color(0.030f, 0.210f, 0.130f);
        private static readonly Color HiVisYellow = new Color(0.980f, 0.840f, 0.040f);   // yellow, not the yellow-green that reads as a green shirt
        private static readonly Color HiVisBand = new Color(0.780f, 0.780f, 0.760f);

        // Dark navy from cap to trouser, black boots.
        private static readonly Garment[] Officer =
        {
            new Garment(AppearanceElementType.Torso, "UniformShirt", Navy, Navy, NavyDark),
            new Garment(AppearanceElementType.Legs, "SuitPants", NavyDark, NavyDark, NavyDark),
            new Garment(AppearanceElementType.Feet, "Boots", Black, BlackSole, Black),
            new Garment(AppearanceElementType.HeadAccessory, "Hat", Navy, Navy, NavyDark),
            new Garment(AppearanceElementType.HairAccessory, Nothing),
            new Garment(AppearanceElementType.TorsoAccessory, Nothing),
            new Garment(AppearanceElementType.LegsAccessory, Nothing),
            new Garment(AppearanceElementType.FeetAccessory, Nothing)
        };

        // White shirt with green trim over green work trousers, black boots, bare-headed.
        private static readonly Garment[] Paramedic =
        {
            new Garment(AppearanceElementType.Torso, "UniformShirt", MedicWhite, MedicWhite, MedicGreen),
            new Garment(AppearanceElementType.Legs, "CargoPantsLong", MedicGreenDark, MedicGreenDark, MedicGreenDark),
            new Garment(AppearanceElementType.Feet, "Boots", Black, BlackSole, Black),
            new Garment(AppearanceElementType.HeadAccessory, Nothing),
            new Garment(AppearanceElementType.HairAccessory, Nothing),
            new Garment(AppearanceElementType.TorsoAccessory, Nothing),
            new Garment(AppearanceElementType.LegsAccessory, Nothing),
            new Garment(AppearanceElementType.FeetAccessory, Nothing)
        };

        private static readonly HashSet<string> Missing = new HashSet<string>();

        /// <summary>True for the looks that wear a uniform; everyone else is dressed at random.</summary>
        public static bool Has(ActorLook look)
        {
            return look == ActorLook.Officer || look == ActorLook.Paramedic;
        }

        /// <summary>
        /// Chooses the uniform's garments on a character whose look has already been rolled, and
        /// records what it recolours. The caller rebuilds the visuals afterwards. A garment the
        /// game no longer ships is logged once and that slot keeps what was rolled.
        /// </summary>
        public static void Wear(AppearanceSetter appearance, ActorLook look, List<Dye> dyes)
        {
            var shirt = default(Shirt);
            Wear(appearance, look, dyes, false, ref shirt);
        }

        /// <summary>The same, remembering the shirt (for HiVis), and bare-headed if the officer is to wear the modelled cap instead of the game's hat.</summary>
        internal static void Wear(AppearanceSetter appearance, ActorLook look, List<Dye> dyes, bool bareHeaded, ref Shirt shirt)
        {
            Garment[] uniform = look == ActorLook.Officer ? Officer : Paramedic;
            shirt = default;
            for (int i = 0; i < uniform.Length; i++)
            {
                Garment garment = uniform[i];
                if (bareHeaded && garment.Slot == AppearanceElementType.HeadAccessory)
                {
                    garment = new Garment(AppearanceElementType.HeadAccessory, Nothing);
                }
                AppearanceElementColor entry = Put(appearance, garment, dyes, out AppearanceElementVariant variant);
                if (garment.Slot == AppearanceElementType.Torso && entry != null)
                {
                    shirt = new Shirt { Entry = entry, Variant = variant, Uniform = garment };
                }
            }
        }

        /// <summary>The shirt a uniform put on a character: what HiVis recolours and puts back.</summary>
        internal struct Shirt
        {
            public AppearanceElementColor Entry;
            public AppearanceElementVariant Variant;
            public bool Bright;
            internal Garment Uniform;
        }

        /// <summary>
        /// Turns an officer's shirt hi-vis yellow (the one working traffic at a cordon), or back to
        /// the uniform's own colour. The reflective bands that make it read as a vest are equipment
        /// (ServiceKit.Vest): a shirt has no part of its own to paint them on. The colour is written to the shirt's own materials, which the
        /// drawn character normally shares, so nothing is rebuilt; only if it does not share them
        /// is the look built again (one mesh merge). Does nothing if it is already so.
        /// </summary>
        internal static void HiVis(AppearanceSetter appearance, ref Shirt shirt, bool on)
        {
            if (shirt.Entry == null || shirt.Bright == on || appearance == null)
            {
                return;
            }
            shirt.Bright = on;
            Garment garment = on ? new Garment(AppearanceElementType.Torso, shirt.Uniform.Name, HiVisYellow, HiVisYellow, HiVisBand) : shirt.Uniform;
            Colour(shirt.Entry, garment);

            SkinnedMeshRenderer source = shirt.Variant != null ? shirt.Variant.GetComponent<SkinnedMeshRenderer>() : null;
            SkinnedMeshRenderer drawn = appearance.lastMergedMeshRenderer;
            if (source != null && drawn != null)
            {
                AppearanceSetter.ApplyColorsToVariantMesh(shirt.Entry, shirt.Variant, source, useSharedMaterials: true);
                source.GetSharedMaterials(Own);
                drawn.GetSharedMaterials(Drawn);
                bool shared = false;
                for (int i = 0; i < Own.Count && !shared; i++)
                {
                    shared = Own[i] != null && i != shirt.Variant.skinMaterialIndex && Drawn.Contains(Own[i]);
                }
                Own.Clear();
                Drawn.Clear();
                if (shared)
                {
                    return;
                }
            }
            if (!_mergeLogged)
            {
                _mergeLogged = true;
                ActorPool.Logger?.Info("People: the drawn character does not share its shirt's material; a hi-vis shirt costs one rebuild of the look each way. (Logged once.)");
            }
            appearance.UpdateVisuals();
        }

        private static readonly List<Material> Own = new List<Material>(4);
        private static readonly List<Material> Drawn = new List<Material>(8);
        private static bool _mergeLogged;

        /// <summary>Puts back every colour a uniform changed on this character.</summary>
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

        private static AppearanceElementColor Put(AppearanceSetter appearance, Garment garment, List<Dye> dyes, out AppearanceElementVariant variant)
        {
            variant = null;
            List<AppearanceElementVariant> variants = appearance.GetElementVariants(garment.Slot);
            if (variants == null || variants.Count == 0)
            {
                return null;   // this body has no such slot
            }
            for (int i = 0; i < variants.Count; i++)
            {
                if (variants[i].name == garment.Name)
                {
                    variant = variants[i];
                    break;
                }
            }
            if (variant == null)
            {
                if (Missing.Add(garment.Slot + "/" + garment.Name))
                {
                    ActorPool.Logger?.Warn($"People: the game has no '{garment.Name}' for {garment.Slot}; uniforms keep whatever was picked there. (Logged once.)");
                }
                return null;
            }

            AppearanceElementData element = null;
            List<AppearanceElementData> elements = appearance.data.elements;
            for (int i = 0; i < elements.Count; i++)
            {
                if (elements[i].type == garment.Slot)
                {
                    element = elements[i];
                    break;
                }
            }
            if (element == null)
            {
                element = new AppearanceElementData { type = garment.Slot };
                elements.Add(element);
            }
            element.variantId = variant.id;
            element.colorId = null;
            if (!garment.Dyed || variant.colors == null)
            {
                return null;
            }

            // The first plain colour the garment has; a patterned (textured) one cannot be recoloured.
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
            dyes.Add(new Dye
            {
                Entry = entry, Base = entry.baseColor, R = entry.r, G = entry.g, B = entry.b, A = entry.a,
                SheenR = entry.specularR, SheenG = entry.specularG, SheenB = entry.specularB, SheenA = entry.specularA
            });
            Colour(entry, garment);
            return entry;
        }

        private static void Colour(AppearanceElementColor entry, Garment garment)
        {
            // Main goes on the base and the first channel in use, Second on the next, Trim on the rest.
            if (entry.hasBaseColor)
            {
                entry.baseColor = garment.Main;
            }
            int used = 0;
            if (entry.hasChannelR)
            {
                entry.r = Shade(garment, used++);
                entry.specularR = Sheen(entry.r);
            }
            if (entry.hasChannelG)
            {
                entry.g = Shade(garment, used++);
                entry.specularG = Sheen(entry.g);
            }
            if (entry.hasChannelB)
            {
                entry.b = Shade(garment, used++);
                entry.specularB = Sheen(entry.b);
            }
            if (entry.hasChannelA)
            {
                entry.a = Shade(garment, used);
                entry.specularA = Sheen(entry.a);
            }
        }

        // The garment's own sheen colour shows at glancing angles (a gold one turned navy cloth brown),
        // so it follows the dye: the same colour, a little lighter, with the alpha the game uses.
        private static Color Sheen(Color dye)
        {
            Color sheen = Color.Lerp(dye, Color.white, 0.12f);
            sheen.a = 0f;
            return sheen;
        }

        private static Color Shade(Garment garment, int channel)
        {
            return channel == 0 ? garment.Main : channel == 1 ? garment.Second : garment.Trim;
        }
    }
}
