using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace RealLifeOverhaul.Leisure
{
    /// <summary>One saved outfit: a name (one of a fixed list, by index) and the look worn in each wardrobe slot, by id.</summary>
    public sealed class SavedOutfit
    {
        public bool Used;
        public int Name;                                   // which of six outfit names (kept with the save: the wardrobe is the game's own screen now, which has no outfits)
        public readonly string[] Ids = new string[8];      // in the order of CharacterCatalogue.Slots; null or empty: the game's own
    }

    /// <summary>
    /// Leisure's part of a save, kept in the save's modData (plain strings under keys the base
    /// game ignores: a save made with the mod loads without it). Best scores by activity, the
    /// landmarks seen, the fish caught, the clothes bought, three outfits, and how much the
    /// player has had to drink. Game-free: read and written through ModState's load and save.
    /// </summary>
    public static class LeisureState
    {
        public const int Outfits = 3;

        private const string KeyBest = "reallife:leisure_best";
        private const string KeyPlays = "reallife:leisure_plays";
        private const string KeySeen = "reallife:leisure_seen";
        private const string KeyFish = "reallife:leisure_fish";
        private const string KeyOwned = "reallife:leisure_owned";
        private const string KeyMustBuy = "reallife:leisure_must_buy";
        private const string KeyOutfit = "reallife:leisure_outfit_";
        private const string KeyDrinks = "reallife:leisure_drinks";
        private const string KeyDrinkTime = "reallife:leisure_drink_time";
        private const string KeySpent = "reallife:leisure_spent";

        private static readonly Dictionary<string, int> Best = new Dictionary<string, int>(12);
        private static readonly Dictionary<string, int> Plays = new Dictionary<string, int>(12);
        private static readonly Dictionary<string, int> Fish = new Dictionary<string, int>(12);
        private static readonly HashSet<string> Seen = new HashSet<string>();
        private static readonly HashSet<string> Owned = new HashSet<string>();
        private static readonly StringBuilder Text = new StringBuilder(256);

        public static readonly SavedOutfit[] Saved = { new SavedOutfit(), new SavedOutfit(), new SavedOutfit() };

        public static bool Loaded { get; private set; }

        /// <summary>Goes up each time a save is read: whoever does something once per save compares it.</summary>
        public static int Loads { get; private set; }

        /// <summary>True if this save had no leisure data when it was read: the director then decides what a new or an old save starts with.</summary>
        public static bool Fresh { get; private set; }

        /// <summary>This save's "clothes must be bought": on for a game started with the mod, off for one that was already going.</summary>
        public static bool MustBuy;

        /// <summary>Drinks still in the blood (each wears off in ninety game minutes) and the game minute the count was last brought up to date.</summary>
        public static float Drinks;
        public static float DrinkTime;

        /// <summary>Everything this save has spent on leisure and clothes.</summary>
        public static int Spent;

        /// <summary>Goes up whenever what is owned changes, for whoever shows it.</summary>
        public static int OwnedVersion { get; private set; }

        public static void Reset()
        {
            Loaded = false;
            Fresh = false;
            Best.Clear();
            Plays.Clear();
            Fish.Clear();
            Seen.Clear();
            Owned.Clear();
            MustBuy = false;
            Drinks = 0f;
            DrinkTime = 0f;
            Spent = 0;
            foreach (SavedOutfit outfit in Saved)
            {
                outfit.Used = false;
                outfit.Name = 0;
                System.Array.Clear(outfit.Ids, 0, outfit.Ids.Length);
            }
            OwnedVersion++;
        }

        public static void ReadSave(Dictionary<string, string> data)
        {
            Reset();
            Fresh = !data.ContainsKey(KeyMustBuy);
            ReadCounts(data, KeyBest, Best);
            ReadCounts(data, KeyPlays, Plays);
            ReadCounts(data, KeyFish, Fish);
            ReadSet(data, KeySeen, Seen);
            ReadSet(data, KeyOwned, Owned);
            MustBuy = data.TryGetValue(KeyMustBuy, out string must) && must == "1";
            Drinks = ReadFloat(data, KeyDrinks);
            DrinkTime = ReadFloat(data, KeyDrinkTime);
            Spent = (int)ReadFloat(data, KeySpent);
            for (int i = 0; i < Outfits; i++)
            {
                if (!data.TryGetValue(KeyOutfit + i, out string line) || string.IsNullOrEmpty(line))
                {
                    continue;
                }
                string[] parts = line.Split('|');
                SavedOutfit outfit = Saved[i];
                outfit.Used = parts.Length > 1 && int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out outfit.Name);
                for (int k = 0; outfit.Used && k < outfit.Ids.Length; k++)
                {
                    outfit.Ids[k] = k + 1 < parts.Length && parts[k + 1].Length > 0 ? parts[k + 1] : null;
                }
            }
            Loaded = true;
            Loads++;
            OwnedVersion++;
        }

        public static void WriteSave(Dictionary<string, string> data)
        {
            if (!Loaded)
            {
                return;
            }
            data[KeyBest] = JoinCounts(Best);
            data[KeyPlays] = JoinCounts(Plays);
            data[KeyFish] = JoinCounts(Fish);
            data[KeySeen] = string.Join(",", Seen);
            data[KeyOwned] = string.Join(",", Owned);
            data[KeyMustBuy] = MustBuy ? "1" : "0";
            data[KeyDrinks] = Drinks.ToString(CultureInfo.InvariantCulture);
            data[KeyDrinkTime] = DrinkTime.ToString(CultureInfo.InvariantCulture);
            data[KeySpent] = Spent.ToString(CultureInfo.InvariantCulture);
            for (int i = 0; i < Outfits; i++)
            {
                SavedOutfit outfit = Saved[i];
                if (!outfit.Used)
                {
                    data[KeyOutfit + i] = string.Empty;
                    continue;
                }
                Text.Length = 0;
                Text.Append(outfit.Name.ToString(CultureInfo.InvariantCulture));
                for (int k = 0; k < outfit.Ids.Length; k++)
                {
                    Text.Append('|').Append(outfit.Ids[k] ?? string.Empty);
                }
                data[KeyOutfit + i] = Text.ToString();
            }
        }

        // ---------------------------------------------------------------- scores

        public static int BestOf(string activity)
        {
            return Best.TryGetValue(activity, out int best) ? best : 0;
        }

        public static int PlaysOf(string activity)
        {
            return Plays.TryGetValue(activity, out int plays) ? plays : 0;
        }

        /// <summary>Counts a finished game and keeps its score if it beats the record. True if it did (and there was a record to beat or this is the first).</summary>
        public static bool Record(string activity, int score)
        {
            Plays[activity] = PlaysOf(activity) + 1;
            if (score > BestOf(activity))
            {
                Best[activity] = score;
                return true;
            }
            return false;
        }

        // ---------------------------------------------------------------- landmarks, fish

        public static bool HasSeen(string landmark) => Seen.Contains(landmark);
        public static int SeenCount => Seen.Count;
        public static bool See(string landmark) => Seen.Add(landmark);

        public static int FishCaught(string fish) => Fish.TryGetValue(fish, out int count) ? count : 0;
        public static int FishKinds => Fish.Count;

        /// <summary>True the first time this kind is caught.</summary>
        public static bool Catch(string fish)
        {
            int had = FishCaught(fish);
            Fish[fish] = had + 1;
            return had == 0;
        }

        // ---------------------------------------------------------------- clothes

        public static bool Owns(string id) => id == null || Owned.Contains(id);
        public static int OwnedCount => Owned.Count;

        public static bool Own(string id)
        {
            if (id == null || !Owned.Add(id))
            {
                return false;
            }
            OwnedVersion++;
            return true;
        }

        public static void SetMustBuy(bool on)
        {
            if (MustBuy != on)
            {
                MustBuy = on;
                OwnedVersion++;
            }
        }

        // ---------------------------------------------------------------- drink

        /// <summary>What is still in the blood at this game minute: a drink wears off in ninety minutes.</summary>
        public static float DrinksAt(float nowMinutes)
        {
            if (Drinks <= 0f)
            {
                return 0f;
            }
            float left = Drinks - UnityEngine.Mathf.Max(0f, nowMinutes - DrinkTime) / 90f;
            return left > 0f ? left : 0f;
        }

        public static void AddDrink(float nowMinutes, float units)
        {
            Drinks = DrinksAt(nowMinutes) + units;
            DrinkTime = nowMinutes;
        }

        // ---------------------------------------------------------------- reading and writing

        private static void ReadCounts(Dictionary<string, string> data, string key, Dictionary<string, int> into)
        {
            if (!data.TryGetValue(key, out string text) || string.IsNullOrEmpty(text))
            {
                return;
            }
            foreach (string entry in text.Split(';'))
            {
                int cut = entry.IndexOf('=');
                if (cut > 0 && int.TryParse(entry.Substring(cut + 1), NumberStyles.Integer, CultureInfo.InvariantCulture, out int value))
                {
                    into[entry.Substring(0, cut)] = value;
                }
            }
        }

        private static string JoinCounts(Dictionary<string, int> from)
        {
            Text.Length = 0;
            foreach (KeyValuePair<string, int> entry in from)
            {
                if (Text.Length > 0)
                {
                    Text.Append(';');
                }
                Text.Append(entry.Key).Append('=').Append(entry.Value.ToString(CultureInfo.InvariantCulture));
            }
            return Text.ToString();
        }

        private static void ReadSet(Dictionary<string, string> data, string key, HashSet<string> into)
        {
            if (data.TryGetValue(key, out string text) && !string.IsNullOrEmpty(text))
            {
                foreach (string id in text.Split(','))
                {
                    if (id.Length > 0)
                    {
                        into.Add(id);
                    }
                }
            }
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
