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
    /// Per-savegame state, stored in the save's modData dictionary, plus the mod's own
    /// happiness modifiers. The game crashes on load if a save contains a happiness modifier
    /// type it doesn't know, so ours are taken out of the save while it is written and kept
    /// in modData instead: a save made with this mod still loads without it.
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

        // Home life and health (Scripts/Home): held on for as long as their cause lasts, or set on waking.
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
        public const string NoPower = "reallife_happinessmodifier_no_power";     // at a home of the player's whose power is cut (Utilities)
        public const string NoWater = "reallife_happinessmodifier_no_water";
        public const string BackPain = "reallife_happinessmodifier_back_pain";
        public const string Burnout = "reallife_happinessmodifier_burnout";
        public const string HomeCooked = "reallife_happinessmodifier_home_cooked";

        // Generic moods shared by the many life events, so each event doesn't need a modifier of its own.
        public const string MoodUp = "reallife_happinessmodifier_mood_up";
        public const string MoodUpBig = "reallife_happinessmodifier_mood_up_big";
        public const string MoodDown = "reallife_happinessmodifier_mood_down";
        public const string MoodDownBig = "reallife_happinessmodifier_mood_down_big";
        public const string Tipsy = "reallife_happinessmodifier_tipsy";   // two or more drinks (Leisure): a small lift for a few hours

        // Civic life (Scripts/Civic): kept up hour by hour while a policy or a site next door gives cause.
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
            (BackPain, -8, 48), (Burnout, -12, 72), (HomeCooked, 5, 8), (Tipsy, 3, 3),
            (CivicClean, 3, 3), (CivicLitter, -3, 3), (CivicFestive, 2, 3), (CivicNoise, -3, 2), (CivicVoted, 5, 24)
        };

        public static IModLogger Logger;
        public static bool Loaded { get; private set; }
        public static bool ModifiersReady { get; private set; }

        /// <summary>Game time (total minutes) until which the player is sick; 0 when healthy.</summary>
        public static float SickUntil;
        public static int LastFriendCallDay;
        public static int LastMomCallDay;
        public static int LastMomGiftDay;

        /// <summary>Money owed for a hit-and-run the player got away from; 0 when nothing is owed.</summary>
        public static int PendingFine;
        /// <summary>Game time (total minutes) at which the pending fine is charged.</summary>
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
            CharacterCatalogue.Leave();   // no save: no picks of the mod's looks (the next save read brings its own)
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
            // Absent in saves from before police pursuits: nothing is owed.
            PendingFine = Mathf.Max(0, (int)ReadFloat(data, KeyPendingFine));
            PendingFineDue = ReadFloat(data, KeyPendingFineDue);

            Leisure.LeisureState.ReadSave(data);   // records, landmarks, the clothes bought, outfits
            CharacterCatalogue.ReadSave(data);   // the player's picks of the mod's own hair and clothes, by id

            // Absent in saves from before the event catalogue: every event simply counts as unseen.
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

            if (data.TryGetValue(KeyModifiers, out string stashed) && !string.IsNullOrEmpty(stashed))
            {
                foreach (string entry in stashed.Split(';'))
                {
                    string[] parts = entry.Split('=');
                    if (parts.Length == 2 && int.TryParse(parts[1], out int hours) && hours > 0
                        && !save.happinessModifiers.Exists(m => m.type == parts[0]))
                    {
                        AddModifier(parts[0], hours);
                    }
                }
            }
            Loaded = true;
            Logger?.Info($"State loaded. sick={IsSick} modifiers={ModifiersReady} seenEvents={SeenEvents.Count}/{LifeEventCatalogue.All.Length}");
        }

        /// <summary>
        /// Called from the game's save event, just before the save is serialised. Returns our
        /// modifiers, which were taken out of the save and must be put back afterwards.
        /// </summary>
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
            data[KeyModifiers] = string.Join(";", parts);
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
            PendingFine += amount;
        }

        public static void ClearPendingFine()
        {
            PendingFine = 0;
            PendingFineDue = 0f;
        }

        /// <summary>Raised when the player has just helped somebody (the "helped" mood, newly earned): Standing hears it through Wiring/StandingLinks.</summary>
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
                    SocialHooks.Helped();   // somebody the player helped may come and say so (Scripts/Social)
                    if (hours < 0)
                    {
                        Law.Reputation.GoodDeed();   // (not when a saved modifier is put back on load: those come with their hours)
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

        // The game has no public way to add a happiness modifier type, so they go into its private registry.
        private static void RegisterModifiers()
        {
            ModifiersReady = false;
            try
            {
                FieldInfo field = typeof(HappinessHelper).GetField("Modifiers", BindingFlags.NonPublic | BindingFlags.Static);
                var registry = field?.GetValue(null) as Dictionary<string, HappinessModifier>;
                if (registry == null)
                {
                    Logger?.Warn("Happiness modifier registry not found; mood effects are disabled.");
                    return;
                }
                foreach ((string type, int amount, int hours) in ModifierDefs)
                {
                    if (registry.ContainsKey(type))
                    {
                        continue;
                    }
                    var modifier = ScriptableObject.CreateInstance<HappinessModifier>();
                    modifier.type = type;
                    modifier.amount = amount;
                    modifier.hoursDuration = hours;
                    modifier.maxHoursDuration = -1;
                    registry[type] = modifier;
                }
                ModifiersReady = true;
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        // ---------------------------------------------------------------- for Meta (the save tools)

        /// <summary>Every key the mod writes into a save's modData starts with this; nothing else of the save's is the mod's.</summary>
        public const string DataPrefix = "reallife:";

        /// <summary>The modData key under which the mod's happiness modifiers wait while a save is written.</summary>
        public const string ModifierStashKey = KeyModifiers;

        /// <summary>True for a happiness modifier type that is one of the mod's, whether this version still has it or not.</summary>
        public static bool IsModModifier(string type)
        {
            return type != null && type.StartsWith(ModifierPrefix, StringComparison.Ordinal);
        }

        /// <summary>True for a modifier type this version of the mod registers.</summary>
        public static bool KnowsModifier(string type)
        {
            foreach ((string known, int _, int _) in ModifierDefs)
            {
                if (known == type)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>Puts the mod's modifier types into the game's registry again if they have gone. Returns whether they are there.</summary>
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
