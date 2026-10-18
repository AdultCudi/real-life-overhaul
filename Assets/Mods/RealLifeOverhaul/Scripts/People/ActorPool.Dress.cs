using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using BAModAPI;
using BigAmbitions.Characters;
using BigAmbitions.Characters.Appearance;
using BigAmbitions.SoundSystem;
using Character;
using Characters;
using Characters.EmojiSystem;
using Extensions;
using Helpers;
using UnityEngine;
using UnityEngine.AI;
using Random = UnityEngine.Random;

namespace RealLifeOverhaul
{
    /// <summary>Dressing a character: the rolled look, the service's look and uniform, its equipment and its height.</summary>
    public partial class ActorPool : MonoBehaviour
    {
        // Everyone's age, build, face and hair are rolled; officers and paramedics then get their uniform and its equipment.
        private static void Dress(StreetActor actor, ActorLook look)
        {
            Looks.Count("people: a street character's look being built (a mesh merge)");   // named for the long-frame line of the session record
            actor.Look = look;
            actor.Stale = false;
            actor.Uses = 0;
            actor.DressedAt = Time.unscaledTime;
            actor.Resize(1f);   // every look is put on, and measured, at the size the game builds it
            AppearanceSetter appearance = actor.Character.appearanceSetter;
            Uniforms.Restore(actor.Dyes);   // the colours of whatever uniform this character wore before
            actor.Shirt = default;
            if (look == ActorLook.Officer || look == ActorLook.Paramedic)
            {
                ServiceLook(appearance);
            }
            else
            {
                appearance.SetRandomAge();
                Gender gender = Random.value < 0.5f ? Gender.Male : Gender.Female;
                appearance.RandomizeElements(gender, SkinColorHelper.GetRandom(), CasualWear);
            }
            bool cap = look == ActorLook.Officer && ServiceKit.CapToBeHad;
            if (Uniforms.Has(look))
            {
                Uniforms.Wear(appearance, look, actor.Dyes, cap, ref actor.Shirt);
            }
            appearance.UpdateVisuals();   // the one mesh merge for this outfit
            Looks.Done();
            if (Equip(actor, look, cap) && actor.Kit.CapRefused)
            {
                // Too much hair for the modelled cap, which hides none: the game's own hat after all, a second merge.
                Uniforms.Restore(actor.Dyes);
                Uniforms.Wear(appearance, look, actor.Dyes, false, ref actor.Shirt);
                Looks.Count("people: an officer's look being built again (the cap did not fit)");
                appearance.UpdateVisuals();
                Looks.Done();
                Equip(actor, look, false);
            }
            if (Uniforms.Has(look))
            {
                Heighten(actor, look, appearance.data.gender == Gender.Male);
            }
        }

        // An officer or a paramedic is scaled to the height the service wants (Stature); said once for each of the four.
        private static void Heighten(StreetActor actor, ActorLook look, bool man)
        {
            float crown = actor.Kit != null ? actor.Kit.Crown : 0f;
            float size = Stature.For(actor.Character.animator, look, man, crown, out float height, out float player);
            actor.Resize(size);
            int bit = (look == ActorLook.Officer ? 1 : 4) << (man ? 0 : 1);
            if ((_heightLogged & bit) == 0)
            {
                _heightLogged |= bit;
                string who = (man ? "male " : "female ") + (look == ActorLook.Officer ? "officer" : "paramedic");
                Logger?.Info(height <= 0f
                    ? $"People: a {who} could not be measured (head {(crown > 0f ? "measured" : "not measured")}); they keep the height the game gave them. (Logged once.)"
                    : FormattableString.Invariant($"People: a {who} is {height:F2} m as the game builds them (crown {crown:F3} m above the head joint; the player is ")
                      + FormattableString.Invariant(
                          $"{(player > 0f ? player.ToString("F2", CultureInfo.InvariantCulture) + " m" : "not measured")}); scaled by {size:F3} to {height * size:F2} m. (Logged once.)"));
            }
        }

        private static int _heightLogged;

        // Officers and paramedics are not rolled like the public: one man and one woman, each the same every time (a fixed
        // seed), the lightest or darkest skin, a fixed build and a short haircut under the cap: the first style matching the
        // names below, else the smallest mesh. What was picked is logged once per sex.
        private const int ManSeed = 1207;

        private const int WomanSeed = 2311;

        private const float Lean = 0.38f;       // CharacterData.fatness: 0.5 is neither fat nor thin, 0 the thinnest the game draws

        private const float Strong = 0.7f;

        private static readonly string[] ManHair = { "buzz", "crew", "short", "fade", "crop" };

        private static readonly string[] WomanHair = { "FemHair06Bun", "bun", "pony", "tied", "bob", "pixie", "short" };

        private static readonly string[] NoBeard = { "none" };

        private static readonly System.Text.StringBuilder Sizes = new System.Text.StringBuilder(256);

        private static SkinColor _lightSkin;

        private static SkinColor _darkSkin;

        private static int _serviceLogged;

        private static void ServiceLook(AppearanceSetter appearance)
        {
            if (_lightSkin == null)
            {
                float low = float.MaxValue, high = float.MinValue;
                for (int seed = 0; seed < 64; seed++)   // the helper only hands tones out by seed: once per session
                {
                    SkinColor tone = SkinColorHelper.GetRandom(seed);
                    float light = tone.color.r * 0.30f + tone.color.g * 0.59f + tone.color.b * 0.11f;
                    if (light > high)
                    {
                        high = light;
                        _lightSkin = tone;
                    }
                    if (light < low)
                    {
                        low = light;
                        _darkSkin = tone;
                    }
                }
            }
            bool man = Random.value < 0.8f;
            appearance.data.ageInDays = 33 * 365;
            appearance.RandomizeElements(man ? Gender.Male : Gender.Female, Random.value < 0.5f ? _lightSkin : _darkSkin, CasualWear, man ? ManSeed : WomanSeed);
            appearance.data.strength = Strong;   // fit: the game rolls both of these 0..1
            appearance.data.fatness = Lean;
            int bit = man ? 1 : 2;
            bool log = (_serviceLogged & bit) == 0;
            Sizes.Clear();
            string hair = Choose(appearance, AppearanceElementType.Hair, man ? ManHair : WomanHair, darkest: true, bare: false, out bool hairNamed, log ? Sizes : null);
            string beard = man ? Choose(appearance, AppearanceElementType.Beard, NoBeard, darkest: false, bare: true, out _, null) : null;
            if (log)
            {
                _serviceLogged |= bit;
                Logger?.Info("People: every " + (man ? "male" : "female") + " officer and paramedic has the same look (seed " + (man ? ManSeed : WomanSeed)
                             + ", lightest or darkest skin tone, build: fatness " + Lean.ToString("F2", CultureInfo.InvariantCulture) + " (lean), strength "
                             + Strong.ToString("F1", CultureInfo.InvariantCulture) + "), haircut "
                             + (hair == null ? "as rolled (the game's styles could not be read)"
                                 : "'" + hair + "' (" + (hairNamed ? "by name" : "the one with the least hair: no style has a name that says it is short") + "), its darkest colour")
                             + "; the game's styles and their sizes in litres: " + Sizes
                             + (man ? "; beard " + (beard != null ? "'" + beard + "'" : "as rolled (no empty choice found)") : string.Empty) + ".");
            }
            Sizes.Clear();
        }

        // Puts on the first variant whose name contains one of the words, in their order, else the one with the least to it
        // (with "bare", no mesh at all counts as the least). Null if the slot has nothing to choose from.
        private static string Choose(AppearanceSetter appearance, AppearanceElementType slot, string[] words, bool darkest, bool bare, out bool named, System.Text.StringBuilder sizes)
        {
            named = false;
            List<AppearanceElementVariant> variants = appearance.GetElementVariants(slot);
            AppearanceElementVariant picked = null;
            for (int w = 0; variants != null && w < words.Length && picked == null; w++)
            {
                for (int i = 0; i < variants.Count && picked == null; i++)
                {
                    if (variants[i] != null && variants[i].name != null && variants[i].name.IndexOf(words[w], StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        picked = variants[i];
                        named = true;
                    }
                }
            }
            if (picked == null || sizes != null)
            {
                AppearanceElementVariant least = Least(variants, bare, sizes);
                if (picked == null)
                {
                    picked = least;
                }
            }
            if (picked == null)
            {
                return null;
            }
            AppearanceElementData element = null;
            List<AppearanceElementData> elements = appearance.data.elements;
            for (int i = 0; i < elements.Count; i++)
            {
                if (elements[i].type == slot)
                {
                    element = elements[i];
                }
            }
            if (element == null)
            {
                element = new AppearanceElementData { type = slot };
                elements.Add(element);
            }
            element.variantId = picked.id;
            AppearanceElementColor colour = null;
            float lightest = float.MaxValue;
            for (int i = 0; picked.colors != null && i < picked.colors.Length; i++)
            {
                AppearanceElementColor entry = picked.colors[i];
                Color shade = entry.hasBaseColor ? entry.baseColor : entry.r;
                float light = darkest ? shade.r + shade.g + shade.b : i;
                if (light < lightest)
                {
                    lightest = light;
                    colour = entry;
                }
            }
            element.colorId = colour != null ? colour.id : null;
            return picked.name;
        }

        // The variant whose mesh takes up the least room: of haircuts, the shortest. One without a mesh only if bare.
        private static AppearanceElementVariant Least(List<AppearanceElementVariant> variants, bool bare, System.Text.StringBuilder sizes)
        {
            AppearanceElementVariant least = null;
            float room = float.MaxValue;
            for (int i = 0; variants != null && i < variants.Count; i++)
            {
                AppearanceElementVariant variant = variants[i];
                if (variant == null)
                {
                    continue;
                }
                var skin = variant.GetComponent<SkinnedMeshRenderer>();
                Mesh mesh = skin != null ? skin.sharedMesh : null;
                bool drawn = mesh != null && mesh.vertexCount > 0;
                Vector3 box = drawn ? Vector3.Scale(mesh.bounds.size, variant.transform.lossyScale) : Vector3.zero;
                float volume = Mathf.Abs(box.x * box.y * box.z);
                if (sizes != null)
                {
                    sizes.Append(sizes.Length > 0 ? ", " : string.Empty).Append(variant.name).Append(' ').Append(drawn ? (volume * 1000f).ToString("F1", CultureInfo.InvariantCulture) : "(no mesh)");
                }
                if ((drawn || bare) && volume < room)
                {
                    room = volume;
                    least = variant;
                }
            }
            return least;
        }

        // The equipment worn with the look: an extra, so if it cannot be fitted they go without, said once. False if none.
        private static bool Equip(StreetActor actor, ActorLook look, bool cap)
        {
            if (actor.Kit == null && !Uniforms.Has(look))
            {
                return false;
            }
            // Fitted at the size the game built the body: the pieces hang on the bones and grow with them.
            float size = actor.Size;
            actor.Resize(1f);
            try
            {
                if (actor.Kit == null)
                {
                    actor.Kit = new ServiceKit();
                }
                actor.Kit.Fit(actor.Character, look, cap);
                actor.Resize(size);
                return true;
            }
            catch (Exception e)
            {
                if (!_kitFailed)
                {
                    _kitFailed = true;
                    Logger?.Error(e);
                    Logger?.Warn("People: uniform equipment could not be fitted (see the error above); that character goes without. (Logged once.)");
                }
                actor.Resize(size);
                try
                {
                    actor.Kit.Remove();
                }
                catch (Exception)
                {
                    // nothing more to be done for it
                }
                return false;
            }
        }

        private static bool _kitFailed;
    }
}
