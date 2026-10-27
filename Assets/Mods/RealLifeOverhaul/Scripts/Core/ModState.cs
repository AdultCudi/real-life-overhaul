using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using BAModAPI;
using Helpers;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// Per-save state in the save's modData, and the mod's own moods (happiness modifiers). The game crashes on loading
    /// a mood type it does not know, so the mod's are taken out of the save while it is written and kept in modData: a
    /// save made with the mod still loads without it.
    /// </summary>
    public static class ModState
    {
        public const string Sick = "reallife_happinessmodifier_sick";
        public const string Injured = "reallife_happinessmodifier_injured";
        public const string Lucky = "reallife_happinessmodifier_lucky";
        public const string Generous = "reallife_happinessmodifier_generous";
        public const string Robbed = "reallife_happinessmodifier_robbed";
        public const string Social = "reallife_happinessmodifier_social";
        public const string Family = "reallife_happinessmodifier_family";
        public const string Helped = "reallife_happinessmodifier_helped";
        public const string Remorse = "reallife_happinessmodifier_remorse";   // someone died under the player's wheels

        // Home life and health (Scripts/Home): held while their cause lasts, or set on waking.
        public const string Unwashed = "reallife_happinessmodifier_unwashed";
        public const string Stressed = "reallife_happinessmodifier_stressed";
        public const string Relaxed = "reallife_happinessmodifier_relaxed";
        public const string MessyHome = "reallife_happinessmodifier_messy_home";
        public const string CosyHome = "reallife_happinessmodifier_cosy_home";
        public const string WellRested = "reallife_happinessmodifier_well_rested";
        public const string Groggy = "reallife_happinessmodifier_groggy";
        public const string ShortOfSleep = "reallife_happinessmodifier_short_of_sleep";
        public const string InShape = "reallife_happinessmodifier_in_shape";
        public const string GoodDiet = "reallife_happinessmodifier_good_diet";
        public const string PoorDiet = "reallife_happinessmodifier_poor_diet";
        public const string NoPower = "reallife_happinessmodifier_no_power";     // at a home whose power is cut (Utilities)
        public const string NoWater = "reallife_happinessmodifier_no_water";
        public const string BackPain = "reallife_happinessmodifier_back_pain";
        public const string Burnout = "reallife_happinessmodifier_burnout";
        public const string HomeCooked = "reallife_happinessmodifier_home_cooked";
        public const string Starving = "reallife_happinessmodifier_starving";   // Permadeath: a day and more on an empty stomach

        // Moods shared by the life events, so each needs no modifier of its own.
        public const string MoodUp = "reallife_happinessmodifier_mood_up";
        public const string MoodUpBig = "reallife_happinessmodifier_mood_up_big";
        public const string MoodDown = "reallife_happinessmodifier_mood_down";
        public const string MoodDownBig = "reallife_happinessmodifier_mood_down_big";
        public const string Tipsy = "reallife_happinessmodifier_tipsy";   // two or more drinks (Leisure): a small lift for a few hours

        // Civic life (Scripts/Civic): kept up hourly while a policy or a site next door gives cause.
        public const string CivicClean = "reallife_happinessmodifier_civic_clean";
        public const string CivicLitter = "reallife_happinessmodifier_civic_litter";
        public const string CivicFestive = "reallife_happinessmodifier_civic_festive";
        public const string CivicNoise = "reallife_happinessmodifier_civic_noise";
        public const string CivicVoted = "reallife_happinessmodifier_civic_voted";

        private const string ModifierPrefix = "reallife_happinessmodifier_";
        private const string KeySickUntil = "reallife:sick_until";
        private const string KeyFriendDay = "reallife:friend_day";
        private const string KeyMomDay = "reallife:mom_day";
        private const string KeyMomGiftDay = "reallife:mom_gift_day";
        private const string KeyModifiers = "reallife:modifiers";
        private const string KeySeenEvents = "reallife:seen_events";
        private const string KeyPendingFine = "reallife:pending_fine";
        private const string KeyPendingFineDue = "reallife:pending_fine_due";

        // type, happiness amount, default hours
        private static readonly (string type, int amount, int hours)[] ModifierDefs =
        {
            (Sick, -15, 36), (Injured, -12, 24), (Lucky, 10, 12), (Generous, 8, 24),
            (Robbed, -12, 12), (Social, 8, 12), (Family, 10, 24), (Helped, 8, 24), (Remorse, -15, 72),
            (MoodUp, 4, 8), (MoodUpBig, 8, 12), (MoodDown, -4, 8), (MoodDownBig, -8, 12),
            (Unwashed, -6, 12), (NoPower, -8, 2), (NoWater, -10, 2), (Stressed, -8, 12), (Relaxed, 4, 12), (MessyHome, -5, 12), (CosyHome, 4, 12),
            (WellRested, 6, 10), (Groggy, -5, 6), (ShortOfSleep, -8, 12), (InShape, 4, 12), (GoodDiet, 4, 12), (PoorDiet, -5, 12),
            (BackPain, -8, 48), (Burnout, -12, 72), (HomeCooked, 5, 8), (Tipsy, 3, 3), (Starving, Permadeath.PermadeathRules.StarveMood, 12),
            (CivicClean, 3, 3), (CivicLitter, -3, 3), (CivicFestive, 2, 3), (CivicNoise, -3, 2), (CivicVoted, 5, 24)
        };

        public static IModLogger Logger;
        public static bool Loaded { get; private set; }
        public static bool ModifiersReady { get; private set; }

        /// <summary>Game time in total minutes until which the player is sick; 0 when healthy.</summary>
        public static float SickUntil;
        public static int LastFriendCallDay;
        public static int LastMomCallDay;
        public static int LastMomGiftDay;

        /// <summary>Money owed for a hit-and-run the player got away from; 0 when nothing is owed.</summary>
        public static int PendingFine;
        /// <summary>Game time in total minutes at which the pending fine is charged.</summary>
        public static float PendingFineDue;

        /// <summary>Ids of the life events this save has already had; see LifeEventDeck.</summary>
        public static readonly HashSet<string> SeenEvents = new HashSet<string>();
        /// <summary>The most recent life event of this session. Not saved.</summary>
        public static string LastEventId;

        public static bool IsSick => SickUntil > 0f && TimeHelper.NowInMinutes() < SickUntil;

        public static void Reset()
        {
            Loaded = false;
            SickUntil = 0f;
            LastFriendCallDay = 0;
            LastMomCallDay = 0;
            LastMomGiftDay = 0;
            PendingFine = 0;
            PendingFineDue = 0f;
            SeenEvents.Clear();
            LastEventId = null;
            Leisure.LeisureState.Reset();
            CharacterCatalogue.Leave();   // no save, no picks of the mod's looks
        }

        public static void Load()
        {
            RegisterModifiers();

            var save = SaveGameManager.Current;
            if (save.modData == null)
            {
                save.modData = new Dictionary<string, string>();
            }
            Dictionary<string, string> data = save.modData;
            SickUntil = ReadFloat(data, KeySickUntil);
            LastFriendCallDay = (int)ReadFloat(data, KeyFriendDay);
            LastMomCallDay = (int)ReadFloat(data, KeyMomDay);
            LastMomGiftDay = (int)ReadFloat(data, KeyMomGiftDay);
            // Absent from older saves: nothing is owed.
            float pending = ReadFloat(data, KeyPendingFine);
            PendingFine = (int)Mathf.Clamp(float.IsNaN(pending) ? 0f : pending, 0f, PendingFineMost);
            if (!float.IsNaN(pending) && pending > PendingFineMost)
            {
                Logger?.Warn("State loaded: the hit-and-run bill in the save, " + pending.ToString("F0", CultureInfo.InvariantCulture)
                             + ", was past any bill the mod writes; it is held to " + PendingFineMost + ".");
            }
            PendingFineDue = ReadFloat(data, KeyPendingFineDue);

            Leisure.LeisureState.ReadSave(data);   // records, landmarks, the clothes bought, outfits
            CharacterCatalogue.ReadSave(data);   // the player's picks of the mod's own hair and clothes, by id

            // Absent from older saves: every event counts as unseen.
            SeenEvents.Clear();
            if (data.TryGetValue(KeySeenEvents, out string seen) && !string.IsNullOrEmpty(seen))
            {
                foreach (string id in seen.Split(','))
                {
                    if (LifeEventCatalogue.Contains(id))
                    {
                        SeenEvents.Add(id);
                    }
                }
            }

            RestoreModifiers(save, data.TryGetValue(KeyModifiers, out string stashed) ? stashed : null);
            Loaded = true;
            Logger?.Info($"State loaded. sick={IsSick} modifiers={ModifiersReady} seenEvents={SeenEvents.Count}/{LifeEventCatalogue.All.Length}");
        }

        /// <summary>From the game's save event, just before the save is written: returns the mod's moods taken out, to put back after.</summary>
        public static List<HappinessModifierData> BeforeSave()
        {
            var removed = new List<HappinessModifierData>();
            var save = SaveGameManager.Current;
            if (!Loaded || save == null)
            {
                return removed;
            }
            if (save.modData == null)
            {
                save.modData = new Dictionary<string, string>();
            }

            var parts = new List<string>();
            for (int i = save.happinessModifiers.Count - 1; i >= 0; i--)
            {
                HappinessModifierData modifier = save.happinessModifiers[i];
                if (modifier.type != null && modifier.type.StartsWith(ModifierPrefix, StringComparison.Ordinal))
                {
                    parts.Add(modifier.type + "=" + modifier.hoursLeft);
                    removed.Add(modifier);
                    save.happinessModifiers.RemoveAt(i);
                }
            }

            Dictionary<string, string> data = save.modData;
            data[KeySickUntil] = SickUntil.ToString(CultureInfo.InvariantCulture);
            data[KeyFriendDay] = LastFriendCallDay.ToString(CultureInfo.InvariantCulture);
            data[KeyMomDay] = LastMomCallDay.ToString(CultureInfo.InvariantCulture);
            data[KeyMomGiftDay] = LastMomGiftDay.ToString(CultureInfo.InvariantCulture);
            data[KeyPendingFine] = PendingFine.ToString(CultureInfo.InvariantCulture);
            data[KeyPendingFineDue] = PendingFineDue.ToString(CultureInfo.InvariantCulture);
            string stash = string.Join(";", parts);
            if (!ModifiersReady && data.TryGetValue(KeyModifiers, out string waiting) && !string.IsNullOrEmpty(waiting))
            {
                // The game's register of mood types could not be reached this session, so the moods waiting were not put
                // back: they keep waiting, each type once so the line does not grow.
                var types = new HashSet<string>(StringComparer.Ordinal);
                var kept = new List<string>();
                foreach (string entry in parts)
                {
                    types.Add(entry.Substring(0, Math.Max(0, entry.IndexOf('='))));
                    kept.Add(entry);
                }
                foreach (string entry in waiting.Split(';'))
                {
                    int eq = entry.IndexOf('=');
                    if (eq > 0 && types.Add(entry.Substring(0, eq)))
                    {
                        kept.Add(entry);
                    }
                }
                stash = string.Join(";", kept);
            }
            data[KeyModifiers] = stash;
            data[KeySeenEvents] = string.Join(",", SeenEvents);
            CharacterCatalogue.WriteSave(data);
            Leisure.LeisureState.WriteSave(data);
            return removed;
        }

        public static void AfterSave(List<HappinessModifierData> removed)
        {
            var save = SaveGameManager.Current;
            if (save == null || removed.Count == 0)
            {
                return;
            }
            save.happinessModifiers.AddRange(removed);
            HappinessHelper.UpdateHappiness();
        }

        /// <summary>Adds to what the player owes; if something is already owed, the earlier date stands.</summary>
        public static void AddPendingFine(int amount, float dueMinutes)
        {
            if (amount <= 0)
            {
                return;
            }
            PendingFineDue = PendingFine > 0 ? Mathf.Min(PendingFineDue, dueMinutes) : dueMinutes;
            PendingFine = WholeMoney.Add(PendingFine, amount, PendingFineMost);
        }

        /// <summary>
        /// The most a hit-and-run bill can come to, at today's prices: a pursuit's damages and the fines for the people hurt,
        /// several times over. A sum kept in a save is held to it.
        /// </summary>
        public static int PendingFineMost => Prices.At(1000000);

        public static void ClearPendingFine()
        {
            PendingFine = 0;
            PendingFineDue = 0f;
        }

        /// <summary>Raised when the "helped" mood is newly earned; Standing hears it through Wiring/StandingLinks.</summary>
        public static event Action HelpedNow;

        public static void AddModifier(string type, int hours = -1)
        {
            if (!ModifiersReady)
            {
                return;
            }
            try
            {
                HappinessHelper.AddModifier(type, hours);
                if (type == Helped)
                {
                    SocialHooks.Helped();   // somebody the player helped may come and say so
                    if (hours < 0)
                    {
                        Law.Reputation.GoodDeed();   // a deed of its own length only
                        HelpedNow?.Invoke();
                    }
                }
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        public static void RemoveModifier(string type)
        {
            if (!ModifiersReady)
            {
                return;
            }
            try
            {
                HappinessHelper.RemoveModifier(type);
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        /// <summary>
        /// Puts a save's moods back into the game's list with their saved hours, written as the game writes a new one but
        /// never through its AddModifier, which Going Public scales on every add. A mood over its cap is trimmed once,
        /// with a check line. COST: once a load, a few operations a mood.
        /// </summary>
        private static void RestoreModifiers(GameInstance save, string stashed)
        {
            if (!ModifiersReady || save.gameVariables == null || save.gameVariables.disableHappiness)
            {
                return;   // as the game's AddModifier: no moods with happiness switched off
            }
            if (save.happinessModifiers == null)
            {
                save.happinessModifiers = new List<HappinessModifierData>();
            }
            Dictionary<string, HappinessModifier> registry = Registry();
            if (registry == null)
            {
                return;
            }
            List<HappinessModifierData> list = save.happinessModifiers;
            int restored = 0, trimmed = 0;
            // Any of the mod's moods the save's own list holds is held to the cap too.
            for (int i = 0; i < list.Count; i++)
            {
                HappinessModifierData held = list[i];
                if (held != null && IsModModifier(held.type) && registry.TryGetValue(held.type, out HappinessModifier known))
                {
                    trimmed += Trim(held, held.hoursLeft, known.maxHoursDuration, "in the save's list");
                }
            }
            if (!string.IsNullOrEmpty(stashed))
            {
                foreach (string entry in stashed.Split(';'))
                {
                    if (!MoodRules.TryRead(entry, out string type, out int hours) || !registry.TryGetValue(type, out HappinessModifier modifier)
                        || Holds(list, type))
                    {
                        continue;   // not a mood, none left, an unregistered type, or one the list has
                    }
                    var data = new HappinessModifierData { type = type, hideDuration = modifier.hideDuration };
                    trimmed += Trim(data, hours, modifier.maxHoursDuration, "saved");
                    list.Add(data);
                    restored++;
                }
            }
            if (restored > 0 || trimmed > 0)
            {
                HappinessHelper.UpdateHappiness();
            }
            if (Meta.Dev.Verbose || trimmed > 0)
            {
                Logger?.Info("Moods check (load): " + restored + " saved mood(s) put back with their saved hours, " + trimmed + " trimmed to their cap: "
                             + (trimmed == 0 ? "PASS" : "a save from before the cap, put right once"));
            }
        }

        // Sets a mood's hours, trimmed to its cap; 1 if it had to be trimmed (and says so), else 0.
        private static int Trim(HappinessModifierData data, int hours, int cap, string where)
        {
            int kept = MoodRules.Kept(hours, cap);
            data.hoursLeft = kept;
            if (kept == hours)
            {
                return 0;
            }
            Logger?.Info("Moods check (load): " + data.type + " " + where + " with " + hours + " h left, over its cap of " + cap + " h: trimmed to " + kept + " h");
            return 1;
        }

        private static bool Holds(List<HappinessModifierData> list, string type)
        {
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] != null && list[i].type == type)
                {
                    return true;
                }
            }
            return false;
        }

        // The game has no public way to add a mood type, so they go into its private registry; family's and travel's go
        // in here too, so a save's moods always find their types.
        private static void RegisterModifiers()
        {
            ModifiersReady = false;
            try
            {
                Dictionary<string, HappinessModifier> registry = Registry();
                if (registry == null)
                {
                    Logger?.Warn("Happiness modifier registry not found; mood effects are disabled.");
                    return;
                }
                Enter(registry, ModifierDefs);
                Enter(registry, RealLifeOverhaul.Family.FamilyModifiers.Defs);
                Enter(registry, TravelEffects.Modifiers);
                ModifiersReady = true;
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        private static Dictionary<string, HappinessModifier> Registry()
        {
            FieldInfo field = GameContract.HappinessRegistry.Field;
            return field?.GetValue(null) as Dictionary<string, HappinessModifier>;
        }

        /// <summary>
        /// Puts a table of the mod's moods into the game's registry, each capped by its length (MoodRules.Cap), which the
        /// game holds every add to, Going Public's scaled ones too. A type already there is left as it is.
        /// </summary>
        internal static void Enter(Dictionary<string, HappinessModifier> registry, (string type, int amount, int hours)[] defs)
        {
            foreach ((string type, int amount, int hours) in defs)
            {
                if (registry.ContainsKey(type))
                {
                    continue;
                }
                var modifier = ScriptableObject.CreateInstance<HappinessModifier>();
                modifier.type = type;
                modifier.amount = amount;
                modifier.hoursDuration = hours;
                modifier.maxHoursDuration = MoodRules.Cap(hours);
                registry[type] = modifier;
            }
        }

        /// <summary>Every key the mod writes into a save's modData starts with this; nothing else of the save's is the mod's.</summary>
        public const string DataPrefix = "reallife:";

        /// <summary>The modData key under which the mod's happiness modifiers wait while a save is written.</summary>
        public const string ModifierStashKey = KeyModifiers;

        /// <summary>True for a mood type of the mod's, whether this version still has it or not.</summary>
        public static bool IsModModifier(string type)
        {
            return type != null && type.StartsWith(ModifierPrefix, StringComparison.Ordinal);
        }

        /// <summary>True for a mood type this version registers: its own, family's and travel's.</summary>
        public static bool KnowsModifier(string type)
        {
            return InTable(ModifierDefs, type) || InTable(RealLifeOverhaul.Family.FamilyModifiers.Defs, type) || InTable(TravelEffects.Modifiers, type);
        }

        private static bool InTable((string type, int amount, int hours)[] defs, string type)
        {
            foreach ((string known, int _, int _) in defs)
            {
                if (known == type)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>Puts the mod's mood types into the game's registry again if they have gone; true when they are there.</summary>
        public static bool RepairModifiers()
        {
            RegisterModifiers();
            return ModifiersReady;
        }

        private static float ReadFloat(Dictionary<string, string> data, string key)
        {
            return data.TryGetValue(key, out string text)
                   && float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out float value)
                ? value
                : 0f;
        }
    }
}
