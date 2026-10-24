using System;
using System.Collections.Generic;
using System.Text;
using RealLifeOverhaul.NewBuildings;

namespace RealLifeOverhaul.Uptown
{
    /// <summary>One office building of the district as the offices' census reads it, made by the game side (LifeGame.Offices). Game-free.</summary>
    public struct OfficeView
    {
        public string Address;     // as the game writes it: the log's and the tie break's
        public string Street;      // the mod's street id and the house number: where the working life's posts are
        public int Number;
        public string Type;        // the business type in it (the game's empty type for none)
        public bool OnOfficeLot;   // on a lot of the plan's office class (else an office the game has for a small lot's size: A3, C1)
        public bool Staffed;       // the game's own staff are on its books
        public bool LaidOut;       // the game finds the layout its registration names, for its type and structure
        public bool ToLet;         // empty and on the market
        public bool Leased;        // one of the city's rival builders' offices (BuildingsRules.Leased)
        public bool Players;       // the player's, rented or owned, or another player's in a Going Public session
        public bool Employs;       // the working life employs a profession at its type
        public float Distance2;    // m squared from the player: the job test takes the nearest
    }

    /// <summary>What the offices' census counts of the district's office buildings (<see cref="OfficesRules.Tally"/>).</summary>
    public sealed class OfficeTally
    {
        public int Offices, OnOfficeLots, Businesses, Staffed, LaidOut, Employers, ToLet, Leased, Players;
        public string Types = string.Empty;   // each business type with its count, in ordinal order of the types: "eventplanningagency 13, lawfirm 15"
    }

    /// <summary>
    /// The rules of Uptown's office buildings as places of work, game-free (EconomyCheck's UptownWorkChecks). The game's own
    /// generator (CityGenerator.InitializeCity, run for the district at its first load and for its buildings new to a save)
    /// lets them as it lets Manhattan's: some kept empty to let by the district's share, the rest given a rival's business of
    /// an office type (law firms, web agencies, design studios, event planners, travel agencies), staffed and laid out. These
    /// rules count what stands, judge it, and pick the office a test sends the player to work at. Every answer comes from the
    /// arguments alone: the same in any order of the buildings.
    /// COST: nothing per frame; a pass over the district's offices when a test is pressed.
    /// </summary>
    public static class OfficesRules
    {
        public const string EmptyType = BuildingsRules.EmptyBusinessType;
        public const string GamePrefix = "ba:businesstype_";   // dropped from a type in the census line, as BizMan's rows name it
        // Points a district's share of offices with a business may stand from the rest of the city's and still be alike: the
        // game keeps 15 to 20 % of a district's to let, and closes and opens businesses daily.
        public const int ShareSlack = 15;

        /// <summary>True when an office has a business in it: a type that is not the game's empty one.</summary>
        public static bool HasBusiness(OfficeView o)
        {
            return !string.IsNullOrEmpty(o.Type) && o.Type != EmptyType;
        }

        /// <summary>The census of the offices: how many, on which lots, with businesses, staffed, laid out, employing, to let, leased, the player's, and the types.</summary>
        public static OfficeTally Tally(IList<OfficeView> offices)
        {
            var t = new OfficeTally();
            if (offices == null)
            {
                return t;
            }
            var types = new SortedDictionary<string, int>(StringComparer.Ordinal);
            for (int i = 0; i < offices.Count; i++)
            {
                OfficeView o = offices[i];
                t.Offices++;
                t.OnOfficeLots += o.OnOfficeLot ? 1 : 0;
                t.ToLet += o.ToLet ? 1 : 0;
                t.Leased += o.Leased ? 1 : 0;
                t.Players += o.Players ? 1 : 0;
                if (!HasBusiness(o) || o.Players)
                {
                    continue;
                }
                t.Businesses++;
                t.Staffed += o.Staffed ? 1 : 0;
                t.LaidOut += o.LaidOut ? 1 : 0;
                t.Employers += o.Employs ? 1 : 0;
                string word = Word(o.Type);
                types[word] = types.TryGetValue(word, out int n) ? n + 1 : 1;
            }
            var line = new StringBuilder(128);
            foreach (KeyValuePair<string, int> each in types)
            {
                line.Append(line.Length > 0 ? ", " : string.Empty).Append(each.Key).Append(' ').Append(each.Value.ToString(System.Globalization.CultureInfo.InvariantCulture));
            }
            t.Types = line.ToString();
            return t;
        }

        /// <summary>A business type as the census line names it: the game's own without its prefix, a mod's whole.</summary>
        public static string Word(string type)
        {
            if (string.IsNullOrEmpty(type))
            {
                return "(none)";
            }
            return type.StartsWith(GamePrefix, StringComparison.Ordinal) ? type.Substring(GamePrefix.Length) : type;
        }

        /// <summary>A whole percentage of a part, halves up; 0 of nothing.</summary>
        public static int Share(int part, int whole)
        {
            return whole <= 0 ? 0 : (int)Math.Round(100.0 * Math.Max(0, part) / whole, MidpointRounding.AwayFromZero);
        }

        /// <summary>
        /// Why the district's offices are not places to work as the game's are (English, for the log), or null: some stand,
        /// some have businesses, every business is staffed and laid out, the working life employs at some, and the share with
        /// a business is within <see cref="ShareSlack"/> points of the rest of the city's (<paramref name="cityOffices"/> and
        /// <paramref name="cityBusinesses"/>; not judged when the city has none).
        /// </summary>
        public static string Verdict(OfficeTally t, int cityOffices, int cityBusinesses)
        {
            if (t == null || t.Offices == 0)
            {
                return "no office building stands in Uptown";
            }
            if (t.Businesses == 0)
            {
                return "none of its " + t.Offices + " office buildings has a business";
            }
            if (t.Staffed < t.Businesses)
            {
                return (t.Businesses - t.Staffed) + " of its office businesses have no staff of the game's";
            }
            if (t.LaidOut < t.Businesses)
            {
                return (t.Businesses - t.LaidOut) + " of its office businesses have no layout the game finds";
            }
            if (t.Employers == 0)
            {
                return "the working life employs at none of its office businesses";
            }
            if (cityOffices > 0)
            {
                int here = Share(t.Businesses, t.Offices - t.Players), there = Share(cityBusinesses, cityOffices);
                if (Math.Abs(here - there) > ShareSlack)
                {
                    return here + " % of its offices have a business against " + there + " % in the rest of the city";
                }
            }
            return null;
        }

        // ---- the opening pass: the units the game's generator leaves empty past the district's share to let

        public const int ShareMost = 100;   // % of a type's units kept to let at most

        /// <summary>
        /// How many of a building type's units to open a business in after the game's generator (LifeGame.OpenUnits): those
        /// empty to let past the share the game keeps to let (<paramref name="sharePercent"/> of <paramref name="units"/>,
        /// rounded as the game's MarkBuildingsForRent rounds it, halves to even), never fewer than none. The generator gives a
        /// unit a business only where an importer or wholesaler rival takes its size, and Uptown is no corporation rival's home,
        /// so shops it leaves empty go well past the district's share.
        /// </summary>
        public static int ToOpen(int units, int emptyToLet, int sharePercent)
        {
            if (units <= 0 || emptyToLet <= 0)
            {
                return 0;
            }
            int share = Math.Max(0, Math.Min(ShareMost, sharePercent));
            int keep = (int)Math.Round(units * share / 100.0, MidpointRounding.ToEven);
            return Math.Max(0, emptyToLet - keep);
        }

        /// <summary>The order the empty units are opened in: by the hash of each lot's id and the save's seed, the same on every machine of a save.</summary>
        public static uint OpenOrder(string lotId, string seed)
        {
            return NewBuildingsRules.Hash((lotId ?? string.Empty) + "|opens|" + (seed ?? string.Empty));
        }

        /// <summary>One of <paramref name="count"/> by a hash (a type, then a business of it, then a rival); -1 of none.</summary>
        public static int Pick(uint hash, int count)
        {
            return count <= 0 ? -1 : (int)(hash % (uint)count);
        }

        /// <summary>
        /// The office a test sends the player to work at, as an index into <paramref name="offices"/>, or -1: of those with a
        /// business of the game's (not a leased builder's, not the player's) that the working life employs at, staffed and laid
        /// out, the nearest; ties by their addresses, ordinal, so the order they come in never matters.
        /// </summary>
        public static int Workplace(IList<OfficeView> offices)
        {
            int best = -1;
            for (int i = 0; offices != null && i < offices.Count; i++)
            {
                OfficeView o = offices[i];
                if (!HasBusiness(o) || o.Leased || o.Players || !o.Employs || !o.Staffed || !o.LaidOut)
                {
                    continue;
                }
                if (best < 0 || o.Distance2 < offices[best].Distance2
                    || (o.Distance2 == offices[best].Distance2 && string.CompareOrdinal(o.Address, offices[best].Address) < 0))
                {
                    best = i;
                }
            }
            return best;
        }
    }
}
