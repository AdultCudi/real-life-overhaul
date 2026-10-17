using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace RealLifeOverhaul.Leisure
{
    /// <summary>One saved outfit: a name from a fixed list, and the look worn in each wardrobe slot.</summary>
    public sealed class SavedOutfit
    {
        public bool Used;
        public int Name;                                   // one of six outfit names, kept in the save
        public readonly string[] Ids = new string[8];      // by CharacterCatalogue.Slots; null or empty: the game's own
    }

    /// <summary>
    /// Leisure's part of the save, plain strings in modData that the base game ignores: records, landmarks seen, fish,
    /// clothes bought, outfits and drink. Read and written through ModState.
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

        /// <summary>Goes up each time a save is read.</summary>
        public static int Loads { get; private set; }

        /// <summary>True if this save had no leisure data when it was read.</summary>
        public static bool Fresh { get; private set; }

        /// <summary>This save's "clothes must be bought".</summary>
        public static bool MustBuy;

        /// <summary>Drinks in the blood at the game minute <see cref="DrinkTime"/>; read through DrinksAt.</summary>
        public static float Drinks;
        public static float DrinkTime;

        public static int Spent;

        /// <summary>Goes up whenever what is owned changes.</summary>
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
                Array.Clear(outfit.Ids, 0, outfit.Ids.Length);
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

        public static int BestOf(string activity)
        {
            return Best.TryGetValue(activity, out int best) ? best : 0;
        }

        public static int PlaysOf(string activity)
        {
            return Plays.TryGetValue(activity, out int plays) ? plays : 0;
        }

        /// <summary>Counts a finished game and keeps its score if it is a new best; true if it was.</summary>
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

        /// <summary>What is still in the blood at this game minute: each drink wears off in ninety.</summary>
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
