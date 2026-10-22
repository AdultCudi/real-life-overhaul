using System.Collections.Generic;
using BigAmbitions.Characters.Appearance;
using UnityEngine;

namespace RealLifeOverhaul.Events
{
    /// <summary>The fire crew's turnout gear, made as <see cref="CityOutfits"/> makes work clothes (black with yellow trim; the helmet is a prop).</summary>
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

        // One garment by name; with a list of dyes, also recoloured.
        private static void Put(AppearanceSetter appearance, AppearanceElementType slot, string name, Color main, Color second, Color trim, List<CityOutfits.Dye> dyes)
        {
            AppearanceElementColor entry = CityOutfits.Choose(appearance, slot, name, dyes != null, appearance.data.elements, out bool missing);
            if (missing && Missing.Add(slot + "/" + name))
            {
                EventDirector.Logger?.Warn($"Fire crew: the game has no '{name}' for {slot}; that slot keeps what was picked. (Logged once.)");
            }
            if (entry == null)
            {
                return;
            }
            // The colour entry is the game's and shared by every firefighter, so each records it as it was before the
            // first dyed it: whoever is undressed last puts the game's colours back, not another's black.
            if (!Originals.TryGetValue(entry, out CityOutfits.Dye original))
            {
                original = CityOutfits.Dye.Of(entry);
                Originals[entry] = original;
            }
            dyes.Add(original);
            CityOutfits.Recolour(entry, main, second, trim);
        }
    }
}
