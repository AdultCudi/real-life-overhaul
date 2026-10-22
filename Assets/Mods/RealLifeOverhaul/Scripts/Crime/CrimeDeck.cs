using System;
using System.Collections.Generic;

namespace RealLifeOverhaul
{
    // Game-free, like CrimeCatalogue.cs: CatalogueCheck compiles both alone to check the catalogue and the dealing.

    /// <summary>Where the player has to be for a crime to be staged around them.</summary>
    [Flags]
    internal enum CrimePlace
    {
        None = 0,
        Street = 1 << 0,      // outdoors, on foot or driving
        OwnShop = 1 << 1,     // inside a business the player runs
        OtherShop = 1 << 2
    }

    /// <summary>What else has to be true; an entry needs all of its flags at once.</summary>
    [Flags]
    internal enum CrimeNeed
    {
        None = 0,
        Night = 1 << 0,
        OnFoot = 1 << 1,
        PatrolCars = 1 << 2,      // the police patrol cars option
        Traffic = 1 << 3,         // traffic to take a car from
        AgainstPlayer = 1 << 4,   // the crimes against you option, and not too soon after the last
        Cash = 1 << 5,            // enough on the player to be worth robbing
        Ragdolls = 1 << 6         // somebody is knocked down
    }

    /// <summary>Who a crime is done to or where, and which test button stages it.</summary>
    internal enum CrimeFamily { Street, Traffic, Shop, Player }

    /// <summary>Which line of the 911 call fits a crime in progress.</summary>
    internal enum ReportKind { None, Crime, Theft, Robbery, Traffic, Accident }

    /// <summary>One way a crime can end, with the lines that can announce it.</summary>
    internal sealed class CrimeEnding
    {
        public readonly string Name;
        public readonly float Odds;
        public readonly bool Arrest;      // a caller who reported it is thanked
        public readonly string[] Texts;

        public CrimeEnding(string name, float odds, bool arrest, params string[] texts)
        {
            Name = name;
            Odds = odds;
            Arrest = arrest;
            Texts = texts;
        }
    }

    /// <summary>One kind of crime: when it can happen, how often, how it can end and what is said about it.</summary>
    internal sealed class CrimeEntry
    {
        public readonly string Id;
        public readonly CrimeFamily Family;
        public CrimePlace Place = CrimePlace.Street;
        public CrimeNeed Needs;
        public int Cast = 2;              // people it needs free in the pool
        public float Weight = 1f;         // share of the deal; 0 is never dealt (started by something else)
        public ReportKind Report = ReportKind.Crime;
        public string Marker = "reallife_crime_marker";
        public string[] Starts = Array.Empty<string>();
        public string[] Near = Array.Empty<string>();      // the player walks up to it
        public string[] Extra = Array.Empty<string>();     // moments of this crime's own
        public CrimeEnding[] Endings = Array.Empty<CrimeEnding>();

        public CrimeEntry(string id, CrimeFamily family)
        {
            Id = id;
            Family = family;
        }

        public bool Dealt => Weight > 0f;

        public bool Fits(CrimePlace place, CrimeNeed have, int freeActors)
        {
            return (Place & place) != 0 && (Needs & have) == Needs && freeActors >= Cast;
        }

        public CrimeEnding Ending(string name)
        {
            foreach (CrimeEnding ending in Endings)
            {
                if (ending.Name == name)
                {
                    return ending;
                }
            }
            return null;
        }

        /// <summary>Every locale key this entry can show.</summary>
        public IEnumerable<string> TextKeys()
        {
            yield return Marker;
            foreach (string key in Starts)
            {
                yield return key;
            }
            foreach (string key in Near)
            {
                yield return key;
            }
            foreach (string key in Extra)
            {
                yield return key;
            }
            foreach (CrimeEnding ending in Endings)
            {
                foreach (string key in ending.Texts)
                {
                    yield return key;
                }
            }
        }
    }

    /// <summary>
    /// Deals crimes like cards: a kind comes again only once every kind that fits has been seen, and no ending or line comes
    /// twice running. Nothing here allocates.
    /// </summary>
    internal static class CrimeDeck
    {
        /// <summary>The next crime for this situation, recorded in <paramref name="seen"/>; null when nothing fits.</summary>
        public static CrimeEntry Draw(IReadOnlyList<CrimeEntry> all, CrimePlace place, CrimeNeed have, int freeActors,
            ISet<string> seen, string lastId, Func<float> random01)
        {
            float fresh = 0f;
            int fitting = 0;
            for (int i = 0; i < all.Count; i++)
            {
                CrimeEntry e = all[i];
                if (!e.Dealt || !e.Fits(place, have, freeActors))
                {
                    continue;
                }
                fitting++;
                if (!seen.Contains(e.Id))
                {
                    fresh += e.Weight;
                }
            }
            if (fitting == 0)
            {
                return null;
            }

            bool reshuffled = fresh <= 0f;
            if (reshuffled)
            {
                // Everything that fits has been seen: those go back in the deck, except the one that ended the round.
                for (int i = 0; i < all.Count; i++)
                {
                    CrimeEntry e = all[i];
                    if (e.Dealt && e.Fits(place, have, freeActors))
                    {
                        seen.Remove(e.Id);
                        if (e.Id != lastId || fitting == 1)
                        {
                            fresh += e.Weight;
                        }
                    }
                }
            }

            float roll = random01() * fresh;
            CrimeEntry picked = null;
            for (int i = 0; i < all.Count; i++)
            {
                CrimeEntry e = all[i];
                if (!e.Dealt || !e.Fits(place, have, freeActors) || seen.Contains(e.Id)
                    || (reshuffled && fitting > 1 && e.Id == lastId))
                {
                    continue;
                }
                picked = e;
                roll -= e.Weight;
                if (roll <= 0f)
                {
                    break;
                }
            }
            if (picked != null)
            {
                seen.Add(picked.Id);
            }
            return picked;
        }

        /// <summary>
        /// An ending by its odds (one with none is reached only by the scene); last time's is left out from three or more,
        /// since with two that would make them take turns.
        /// </summary>
        public static CrimeEnding DrawEnding(CrimeEnding[] endings, string lastName, Func<float> random01)
        {
            int rollable = 0;
            for (int i = 0; i < endings.Length; i++)
            {
                if (endings[i].Odds > 0f)
                {
                    rollable++;
                }
            }
            if (rollable == 0)
            {
                return null;
            }
            bool skipLast = rollable >= 3;
            float total = 0f;
            for (int i = 0; i < endings.Length; i++)
            {
                if (endings[i].Odds > 0f && !(skipLast && endings[i].Name == lastName))
                {
                    total += endings[i].Odds;
                }
            }
            float roll = random01() * total;
            CrimeEnding picked = null;
            for (int i = 0; i < endings.Length; i++)
            {
                if (endings[i].Odds <= 0f || (skipLast && endings[i].Name == lastName))
                {
                    continue;
                }
                picked = endings[i];
                roll -= endings[i].Odds;
                if (roll <= 0f)
                {
                    break;
                }
            }
            return picked;
        }

        /// <summary>One line out of several, never the last one shown when there is another.</summary>
        public static string DrawText(string[] texts, string last, Func<float> random01)
        {
            if (texts == null || texts.Length == 0)
            {
                return null;
            }
            if (texts.Length == 1)
            {
                return texts[0];
            }
            int index = Math.Min((int)(random01() * texts.Length), texts.Length - 1);
            if (texts[index] == last)
            {
                index = (index + 1) % texts.Length;
            }
            return texts[index];
        }
    }
}
