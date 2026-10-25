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
    /// never touched, and every outfit changed is put back as it was. One person at a look (every few seconds), never one
    /// on screen, from the game's list of people walking; the game merges their meshes again as it does when it dresses
    /// them, which is its own work (named in the session record), the only allocation of a dressing. Nothing of the mod's
    /// is made for it: the outfit worn is kept by reference and the change is written through two kept pieces, which the
    /// game copies. Off by default.
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

        private const string DressWhat = "seasons: a person dressed for the cold (the game's own mesh merge)";
        private const string UndressWhat = "seasons: a person's own clothes put back (the game's own mesh merge)";

        // The top and trousers each person wore before, by reference: the game drops them from its list and keeps copies.
        private static readonly Dictionary<AppearanceSetter, Worn> Dressed = new Dictionary<AppearanceSetter, Worn>(64);
        private static readonly Dictionary<string, bool> Warmth = new Dictionary<string, bool>(64);   // a variant's id: warm for its kind
        private static readonly List<AppearanceSetter> Undress = new List<AppearanceSetter>(64);
        private static readonly List<AppearanceElementData> Change = new List<AppearanceElementData>(2);
        // The warm pieces written: the game copies what it is handed (UpdateElements), so the same two serve every person.
        private static readonly AppearanceElementData WarmTop = new AppearanceElementData();
        private static readonly AppearanceElementData WarmLegs = new AppearanceElementData();
        private static bool _coatsFailed;

        // What a person wore before the cold; both null when they were warm already and nothing was changed.
        private struct Worn
        {
            public AppearanceElementData Top, Legs;
        }

        public static int CoatsOn => Dressed.Count;

        /// <summary>
        /// At a look: one more person off screen dressed for <paramref name="season"/> while <paramref name="wanted"/>
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
                    return;   // one a look
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

        // The game's own list is read, never copied: what was worn stays as the game had it once it drops it for the change.
        private static void Dress(AppearanceSetter look, bool legs)
        {
            List<AppearanceElementData> own = look.data != null ? look.data.elements : null;
            if (own == null)
            {
                Dressed[look] = default(Worn);
                return;
            }
            Change.Clear();
            var worn = new Worn
            {
                Top = Warm(look, own, AppearanceElementType.Torso, WarmTops, null, WarmTop),
                Legs = legs ? Warm(look, own, AppearanceElementType.Legs, LongLegs, ShortLegs, WarmLegs) : null
            };
            Dressed[look] = worn;   // both null: already warm, nothing to put back
            if (Change.Count > 0)
            {
                int mark = SessionWatch.Mark(DressWhat);
                look.UpdateElements(Change);
                SessionWatch.Done(mark);
            }
        }

        // A warm variant of one kind of element in the style of the one worn, written into <paramref name="into"/> and
        // handed for the change, if the one worn is not warm already. Returns the one worn when it is to be changed.
        private static AppearanceElementData Warm(AppearanceSetter look, List<AppearanceElementData> own, AppearanceElementType type, string[] warm, string[] not, AppearanceElementData into)
        {
            AppearanceElementData worn = null;
            for (int i = 0; i < own.Count && worn == null; i++)
            {
                worn = own[i] != null && own[i].type == type ? own[i] : null;
            }
            List<AppearanceElementVariant> variants = worn != null ? look.GetElementVariants(type) : null;
            if (variants == null)
            {
                return null;
            }
            AppearanceElementVariant current = null;
            for (int i = 0; i < variants.Count && current == null; i++)
            {
                current = variants[i] != null && variants[i].id == worn.variantId ? variants[i] : null;
            }
            if (current == null || IsWarm(current, warm, not))
            {
                return null;
            }
            int choices = 0;
            for (int i = 0; i < variants.Count; i++)
            {
                choices += Fits(variants[i], current, warm, not) ? 1 : 0;
            }
            if (choices == 0)
            {
                return null;
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
                into.type = type;
                into.variantId = chosen.id;
                into.colorId = colour;
                Change.Add(into);
                return worn;
            }
            return null;
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
            foreach (KeyValuePair<AppearanceSetter, Worn> one in Dressed)
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
                Worn worn = Dressed[look];
                Dressed.Remove(look);
                Change.Clear();
                if (worn.Top != null)
                {
                    Change.Add(worn.Top);
                }
                if (worn.Legs != null)
                {
                    Change.Add(worn.Legs);
                }
                if (look != null && Change.Count > 0)
                {
                    int mark = SessionWatch.Mark(UndressWhat);
                    look.UpdateElements(Change);
                    SessionWatch.Done(mark);
                }
            }
            Change.Clear();
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
