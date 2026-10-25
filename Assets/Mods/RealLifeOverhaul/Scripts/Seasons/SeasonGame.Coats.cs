using System.Collections.Generic;
using BigAmbitions.Characters.Appearance;
using Streets.Pedestrians;
using UnityEngine;

namespace RealLifeOverhaul.Seasons
{
    /// <summary>
    /// People in the street dressed for the cold, from the game's own clothes alone: in autumn a warm top, in winter a warm
    /// top and long trousers, chosen among the variants the person's own model has, in their own outfit's style (its
    /// tags). Only the clothes' elements change (the game's AppearanceSetter.UpdateElements); body, face, hair and skin are
    /// never touched, and every outfit changed is put back as it was. One person a second, never one on screen, from the
    /// game's list of people walking; the game merges their meshes again as it does when it dresses them. Off by default.
    /// </summary>
    public static partial class SeasonGame
    {
        private const int UndressASecond = 3;   // outfits put back a second when the cold is over, so no frame merges many
        private const float OutOfSight = 45f;   // metres: without a merged mesh to ask, a person this far off is taken as off screen

        // The game's warm pieces, by their variants' names (MaleSweater, FemHoodie, SuitJacket, HighNeckShirt, Jeans,
        // LongCargoPants, SuitPants): the game has no coats.
        private static readonly string[] WarmTops = { "Sweater", "Hoodie", "Jacket", "HighNeck", "Coat", "Cardigan", "Blazer" };
        private static readonly string[] LongLegs = { "Jeans", "Pants", "Trousers" };
        private static readonly string[] ShortLegs = { "Short", "Skirt" };

        private static readonly Dictionary<AppearanceSetter, List<AppearanceElementData>> Dressed = new Dictionary<AppearanceSetter, List<AppearanceElementData>>(64);
        private static readonly Dictionary<string, bool> Warmth = new Dictionary<string, bool>(64);   // a variant's id: warm for its kind
        private static readonly List<AppearanceSetter> Undress = new List<AppearanceSetter>(64);
        private static readonly List<AppearanceElementData> Change = new List<AppearanceElementData>(2);
        private static bool _coatsFailed;

        public static int CoatsOn => Dressed.Count;

        /// <summary>
        /// Once a second: one more person off screen dressed for <paramref name="season"/> while <paramref name="wanted"/>
        /// and it is autumn or winter; otherwise a few of those dressed put back.
        /// </summary>
        public static void Coats(int season, bool wanted)
        {
            if (_coatsFailed)
            {
                return;
            }
            bool cold = wanted && (season == CalendarRules.Autumn || season == CalendarRules.Winter);
            if (!cold)
            {
                PutBack(UndressASecond);
                return;
            }
            var city = InstanceBehavior<CityManager>.Instance;
            List<Pedestrian> walking = city != null && city.pedestrianSpawner != null ? city.pedestrianSpawner.activePedestrians : null;
            if (walking == null)
            {
                return;
            }
            try
            {
                for (int i = 0; i < walking.Count; i++)
                {
                    AppearanceSetter look = walking[i] != null && walking[i].tpc != null ? walking[i].tpc.appearanceSetter : null;
                    if (look == null || Dressed.ContainsKey(look) || Seen(look))
                    {
                        continue;
                    }
                    Dress(look, season == CalendarRules.Winter);
                    return;   // one a second
                }
            }
            catch (System.Exception e)
            {
                _coatsFailed = true;
                SeasonDirector.Logger?.Error(e);
                SeasonDirector.Logger?.Warn("Seasons: people's clothes cannot be changed in this build; they keep the game's own this session.");
                RestoreCoats();
            }
        }

        // On screen now: the merged mesh's own answer, else nearer than the game spawns people (50 to 90 m off).
        private static bool Seen(AppearanceSetter look)
        {
            if (look.lastMergedMeshRenderer != null)
            {
                return look.lastMergedMeshRenderer.isVisible;
            }
            return (look.transform.position - Player).sqrMagnitude < OutOfSight * OutOfSight;
        }

        private static void Dress(AppearanceSetter look, bool legs)
        {
            List<AppearanceElementData> own = look.GetCopyOfCurrentElements();
            Change.Clear();
            Warm(look, own, AppearanceElementType.Torso, WarmTops, null);
            if (legs)
            {
                Warm(look, own, AppearanceElementType.Legs, LongLegs, ShortLegs);
            }
            var kept = new List<AppearanceElementData>(2);
            for (int i = 0; i < own.Count; i++)
            {
                if (own[i] != null && (own[i].type == AppearanceElementType.Torso || own[i].type == AppearanceElementType.Legs))
                {
                    kept.Add(own[i]);
                }
            }
            Dressed[look] = Change.Count > 0 ? kept : null;   // null: already warm, nothing to put back
            if (Change.Count > 0)
            {
                look.UpdateElements(Change);
            }
        }

        // A warm variant of one kind of element in the style of the one worn, if the one worn is not warm already.
        private static void Warm(AppearanceSetter look, List<AppearanceElementData> own, AppearanceElementType type, string[] warm, string[] not)
        {
            AppearanceElementData worn = null;
            for (int i = 0; i < own.Count && worn == null; i++)
            {
                worn = own[i] != null && own[i].type == type ? own[i] : null;
            }
            List<AppearanceElementVariant> variants = worn != null ? look.GetElementVariants(type) : null;
            if (variants == null)
            {
                return;
            }
            AppearanceElementVariant current = null;
            for (int i = 0; i < variants.Count && current == null; i++)
            {
                current = variants[i] != null && variants[i].id == worn.variantId ? variants[i] : null;
            }
            if (current == null || IsWarm(current, warm, not))
            {
                return;
            }
            int choices = 0;
            for (int i = 0; i < variants.Count; i++)
            {
                choices += Fits(variants[i], current, warm, not) ? 1 : 0;
            }
            if (choices == 0)
            {
                return;
            }
            // The same pick for the same outfit every time: the worn variant's id decides, no chance of the mod's own.
            int pick = (int)((uint)StableHash(worn.variantId) % (uint)choices);
            for (int i = 0; i < variants.Count; i++)
            {
                if (!Fits(variants[i], current, warm, not) || pick-- > 0)
                {
                    continue;
                }
                AppearanceElementVariant chosen = variants[i];
                string colour = chosen.colors != null && chosen.colors.Length > 0 && chosen.colors[0] != null
                    ? chosen.colors[(int)((uint)StableHash(worn.colorId ?? string.Empty) % (uint)chosen.colors.Length)].id : worn.colorId;
                Change.Add(new AppearanceElementData { type = type, variantId = chosen.id, colorId = colour });
                return;
            }
        }

        private static bool Fits(AppearanceElementVariant variant, AppearanceElementVariant current, string[] warm, string[] not)
        {
            return variant != null && variant != current && IsWarm(variant, warm, not) && Shares(variant.tags, current.tags);
        }

        private static bool IsWarm(AppearanceElementVariant variant, string[] warm, string[] not)
        {
            if (variant.id != null && Warmth.TryGetValue(variant.id, out bool known))
            {
                return known;
            }
            string name = variant.gameObject.name;
            bool yes = Has(name, warm) && (not == null || !Has(name, not));
            if (variant.id != null)
            {
                Warmth[variant.id] = yes;
            }
            return yes;
        }

        private static bool Has(string name, string[] words)
        {
            for (int i = 0; i < words.Length; i++)
            {
                if (name.IndexOf(words[i], System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }
            return false;
        }

        // In the worn outfit's style: casual for casual, formal for formal (a variant with no tags fits any).
        private static bool Shares(AppearanceTag[] a, AppearanceTag[] b)
        {
            if (a == null || b == null || a.Length == 0 || b.Length == 0)
            {
                return true;
            }
            for (int i = 0; i < a.Length; i++)
            {
                for (int j = 0; j < b.Length; j++)
                {
                    if (a[i] == b[j] && a[i] != AppearanceTag.Uniform && a[i] != AppearanceTag.SwimmingPool)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        private static int StableHash(string text)
        {
            unchecked
            {
                int hash = 23;
                for (int i = 0; i < text.Length; i++)
                {
                    hash = hash * 31 + text[i];
                }
                return hash;
            }
        }

        // Up to <paramref name="most"/> outfits put back as they were.
        private static void PutBack(int most)
        {
            if (Dressed.Count == 0)
            {
                return;
            }
            Undress.Clear();
            foreach (KeyValuePair<AppearanceSetter, List<AppearanceElementData>> one in Dressed)
            {
                if (Undress.Count >= most)
                {
                    break;
                }
                Undress.Add(one.Key);
            }
            for (int i = 0; i < Undress.Count; i++)
            {
                AppearanceSetter look = Undress[i];
                List<AppearanceElementData> own = Dressed[look];
                Dressed.Remove(look);
                if (look != null && own != null && own.Count > 0)
                {
                    look.UpdateElements(own);
                }
            }
        }

        /// <summary>Every outfit changed put back at once. Safe at any time.</summary>
        public static void RestoreCoats()
        {
            try
            {
                PutBack(int.MaxValue);
            }
            catch (System.Exception e)
            {
                SeasonDirector.Logger?.Error(e);
                Dressed.Clear();
            }
        }
    }
}
