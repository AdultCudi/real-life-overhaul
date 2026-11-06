using System;
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
    /// never touched, and every outfit changed is put back as it was. The game merges a person's meshes again for each
    /// change, its own work and some 1.5 MB of arrays each: up to 0.52.1 one person was dressed at every look in play (20
    /// a minute until the pool was dressed; 241 to 528 KB a second under the seasons' tick in the owner's session of
    /// 25 Oct 2026). Now a look only notes that someone waits, and the dressing (or the putting back once the cold is over)
    /// is done behind the black screen of the next walk out to the street (Core/ScenePrewarm), at most
    /// <see cref="DoorMost"/> people a door. Cost in play: a walk over the game's list of walkers at a look, nothing
    /// allocated. Nothing of the mod's is made for a dressing: the outfit worn is kept by reference and the change is
    /// written through two kept pieces, which the game copies. Off by default.
    /// </summary>
    public static partial class SeasonGame
    {
        private const int DoorMost = 12;        // people dressed or put back behind one black screen: a dozen merges where nothing shows

        // The warm tops are WarmthRules'; the legs' (Jeans, LongCargoPants, SuitPants) are the street's alone.
        private static readonly string[] LongLegs = { "Jeans", "Pants", "Trousers" };
        private static readonly string[] ShortLegs = { "Short", "Skirt" };

        private const string DoorWhat = "seasons: people in the street dressed for the cold, or their own clothes put back (behind the black screen)";
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
        private static bool _cold, _winter;     // as the last look found it, for the work at the door
        private static bool _asked;             // the work is with Core/ScenePrewarm for the next door
        private static int _doorLeft;           // merges still allowed to this asking: one a black frame (RLO-PERF-024)
        private static Func<bool> _atDoor;      // made once

        // What a person wore before the cold; both null when they were warm already and nothing was changed.
        private struct Worn
        {
            public AppearanceElementData Top, Legs;
        }

        public static int CoatsOn => Dressed.Count;

        /// <summary>
        /// At a look: whether someone waits to be dressed for <paramref name="season"/> while <paramref name="wanted"/>
        /// and it is autumn or winter, or to have their own clothes back; if so the work is asked of the next black screen.
        /// </summary>
        public static void Coats(int season, bool wanted)
        {
            if (_coatsFailed)
            {
                return;
            }
            _cold = wanted && (season == CalendarRules.Autumn || season == CalendarRules.Winter);
            _winter = season == CalendarRules.Winter;
            if (_asked || !(_cold ? Undressed() : Dressed.Count > 0))
            {
                return;
            }
            _asked = true;
            ScenePrewarm.Again(DoorWhat, _atDoor ??= AtDoor);
        }

        // Someone in the game's list of walkers not dressed yet.
        private static bool Undressed()
        {
            List<Pedestrian> walking = Walking();
            for (int i = 0; walking != null && i < walking.Count; i++)
            {
                AppearanceSetter look = walking[i] != null && walking[i].tpc != null ? walking[i].tpc.appearanceSetter : null;
                if (look != null && !Dressed.ContainsKey(look))
                {
                    return true;
                }
            }
            return false;
        }

        private static List<Pedestrian> Walking()
        {
            var city = InstanceBehavior<CityManager>.Instance;
            return city != null && city.pedestrianSpawner != null ? city.pedestrianSpawner.activePedestrians : null;
        }

        // Behind the black screen of a walk out (Core/ScenePrewarm): the merges no frame of play may pay for, one a frame
        // (each is a mesh merge of 1 to 2 ms; a dozen in one frame were most of beta 6's 13 ms frame), at most DoorMost to
        // an asking. Done for this door whatever is left; the next look asks again.
        private static bool AtDoor()
        {
            if (_coatsFailed)
            {
                _asked = false;
                return true;
            }
            try
            {
                if (_doorLeft <= 0)
                {
                    _doorLeft = DoorMost;   // a new asking
                    Forget();
                }
                int changed = _cold ? DressSome(1) : PutBack(1);
                if (changed > 0 && --_doorLeft > 0)
                {
                    ScenePrewarm.NextFrame();   // the next in the next frame while the screen is black
                    return false;
                }
            }
            catch (Exception e)
            {
                _coatsFailed = true;
                SeasonDirector.Logger?.Error(e);
                SeasonDirector.Logger?.Warn("Seasons: people's clothes cannot be changed in this build; they keep the game's own this session.");
                RestoreCoats();
            }
            _doorLeft = 0;
            _asked = false;
            return true;
        }

        // Up to <paramref name="most"/> walkers dressed; how many were.
        private static int DressSome(int most)
        {
            int dressed = 0;
            List<Pedestrian> walking = Walking();
            for (int i = 0; walking != null && i < walking.Count && dressed < most; i++)
            {
                AppearanceSetter look = walking[i] != null && walking[i].tpc != null ? walking[i].tpc.appearanceSetter : null;
                if (look != null && !Dressed.ContainsKey(look))
                {
                    Dress(look, _winter);
                    dressed++;
                }
            }
            return dressed;
        }

        // A walker the pool destroyed (one released past its most) leaves its key: nothing of it is to be put back.
        private static void Forget()
        {
            Undress.Clear();
            foreach (KeyValuePair<AppearanceSetter, Worn> one in Dressed)
            {
                if (one.Key == null)
                {
                    Undress.Add(one.Key);
                }
            }
            for (int i = 0; i < Undress.Count; i++)
            {
                Dressed.Remove(Undress[i]);
            }
            Undress.Clear();
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
                Top = Warm(look, own, AppearanceElementType.Torso, WarmthRules.WarmTops, null, WarmTop),
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
            bool yes = WarmthRules.Has(name, warm) && (not == null || !WarmthRules.Has(name, not));
            if (variant.id != null)
            {
                Warmth[variant.id] = yes;
            }
            return yes;
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

        // Up to <paramref name="most"/> outfits put back as they were; how many were.
        private static int PutBack(int most)
        {
            if (Dressed.Count == 0)
            {
                return 0;
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
                // A city unloading has destroyed some people's head parts before this runs: the game's UpdateVisuals reads the head's mesh and
                // throws on a dead one, so a look whose parts are gone is left alone (two Unity-null tests, once per outfit put back).
                if (look != null && Change.Count > 0 && LookIntact(look))
                {
                    int mark = SessionWatch.Mark(UndressWhat);
                    look.UpdateElements(Change);
                    SessionWatch.Done(mark);
                }
            }
            Change.Clear();
            int back = Undress.Count;
            Undress.Clear();
            return back;
        }

        // True while the person's look still has the head part and mesh the game's UpdateHeadBlendshapes reads.
        internal static bool LookIntact(AppearanceSetter look)
        {
            if (look == null || look.data == null)
            {
                return false;
            }
            SkinnedMeshRenderer head = look.HeadMesh;
            return head != null && head.sharedMesh != null;
        }

        /// <summary>Every outfit changed put back at once. Safe at any time.</summary>
        public static void RestoreCoats()
        {
            _asked = false;   // the city's pieces go with it (Core/ScenePrewarm): asked again at the next look
            _doorLeft = 0;
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
