using System;
using System.Collections.Generic;
using System.Text;

namespace RealLifeOverhaul.TestShops
{
    /// <summary>One business the test tool may take over, read from the save once, on the press. The flags are the game's own facts.</summary>
    public struct ShopCandidate
    {
        public string Kind;             // the business type's key: one of each is wanted
        public float Distance;          // squared flat metres from the player to the building: only the order counts
        public bool Rented;             // RentedByPlayer
        public bool Owned;              // BuildingOwnedByPlayer
        public bool CanBeTakenOver;     // the building type's canbetakenover tag
        public bool SpecialService;     // the building is a special service (hospital, police)
        public bool Empty;              // the business type is "empty"
        public bool Office;             // an office: the game wants the Office Businesses diploma
        public bool SpecialRivalActive; // owned by a special rival who is active: the game refuses it
        public bool Unavailable;        // a DLC the building needs, or the building the player stands in: the game refuses both
    }

    /// <summary>What a press chose, with the skips that the notice may need to explain.</summary>
    public sealed class ShopPick
    {
        public readonly List<int> Chosen = new List<int>(16);   // indices into the candidates, nearest first
        public int OfficesLeftOut;                               // offices the missing diploma kept out
        public int RivalsLeftOut;                                // buildings of an active special rival

        public void Clear()
        {
            Chosen.Clear();
            OfficesLeftOut = 0;
            RivalsLeftOut = 0;
        }
    }

    /// <summary>
    /// The pick of the test tool that takes businesses over (Options > Tests), game-free so a test holds it. The filter is the game
    /// console's own for "OvertakeRandomBusinesses", with the two refusals of the game's offer path added. Runs only on a press.
    /// </summary>
    public static class TestShopsRules
    {
        public const int NoticeMax = 240;   // characters: the longest notice the game's toast takes

        /// <summary>The console's filter: a business of someone else, in a building that may be taken over, and not an empty shop.</summary>
        public static bool Takeable(in ShopCandidate c)
        {
            return !c.Rented && !c.Owned && c.CanBeTakenOver && !c.SpecialService && !c.Empty;
        }

        /// <summary>
        /// Fills <paramref name="into"/> with the nearest takeable business of each kind (<paramref name="oneOfEach"/>), or the nearest
        /// one. Equal distances go to the earlier entry in the save, the same on every machine.
        /// </summary>
        public static void Pick(IList<ShopCandidate> all, bool oneOfEach, bool officeDiploma, ShopPick into)
        {
            into.Clear();
            if (all == null)
            {
                return;
            }
            var best = new Dictionary<string, int>(32, StringComparer.Ordinal);
            int nearest = -1;
            for (int i = 0; i < all.Count; i++)
            {
                ShopCandidate c = all[i];
                if (!Takeable(c) || c.Unavailable || string.IsNullOrEmpty(c.Kind))
                {
                    continue;
                }
                if (c.SpecialRivalActive)
                {
                    into.RivalsLeftOut++;
                    continue;
                }
                if (c.Office && !officeDiploma)
                {
                    into.OfficesLeftOut++;
                    continue;
                }
                if (nearest < 0 || c.Distance < all[nearest].Distance)
                {
                    nearest = i;
                }
                int held;
                if (!best.TryGetValue(c.Kind, out held) || c.Distance < all[held].Distance)
                {
                    best[c.Kind] = i;
                }
            }
            if (!oneOfEach)
            {
                if (nearest >= 0)
                {
                    into.Chosen.Add(nearest);
                }
                return;
            }
            foreach (KeyValuePair<string, int> pair in best)
            {
                into.Chosen.Add(pair.Value);
            }
            into.Chosen.Sort((a, b) =>
            {
                int byDistance = all[a].Distance.CompareTo(all[b].Distance);
                return byDistance != 0 ? byDistance : a.CompareTo(b);
            });
        }

        /// <summary>True when the tool must do nothing: another player's things share the save in a Going Public session.</summary>
        public static bool Refused(bool inGoingPublicSession) => inGoingPublicSession;

        /// <summary>
        /// The kinds as one line that fits <paramref name="budget"/> characters: whole names, then "and N more" (the text
        /// <paramref name="moreFormat"/> with {count}) for the rest.
        /// </summary>
        public static string Kinds(IList<string> names, int budget, string moreFormat)
        {
            string all = string.Join(", ", names);
            if (all.Length <= budget)
            {
                return all;
            }
            for (int keep = names.Count - 1; keep >= 0; keep--)
            {
                string head = Head(names, keep);
                string more = moreFormat.Replace("{count}", (names.Count - keep).ToString());
                string line = keep == 0 ? more : head + " " + more;
                if (line.Length <= budget)
                {
                    return line;
                }
            }
            return moreFormat.Replace("{count}", names.Count.ToString());
        }

        private static string Head(IList<string> names, int count)
        {
            var head = new StringBuilder();
            for (int i = 0; i < count; i++)
            {
                head.Append(i == 0 ? string.Empty : ", ").Append(names[i]);
            }
            return head.ToString();
        }
    }
}
