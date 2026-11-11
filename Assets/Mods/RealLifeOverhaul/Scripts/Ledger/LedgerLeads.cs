using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Ledger
{
    /// <summary>
    /// The City Ledger's stories of a sector's lead changing hands: the most valuable listed company of each part of the market
    /// is the leader of its field, and the morning another company passes it, by a margin and with the leader a major
    /// company, the paper names both. A player's company, a rival tycoon's holding and the city's own companies are each told
    /// in a story of their own. Read from the market's last close and from who led each sector at the edition before
    /// (<see cref="LedgerState.Leads"/>), so a reload never prints a story twice. Game-free; the cost is one pass over the
    /// listed companies once a morning, nothing per frame.
    /// </summary>
    public static class LedgerLeads
    {
        private const string P = "reallife";   // in parts: keys' beginnings, not keys

        public static readonly string LeadOwn = P + "_ledger_lead_own";       // a player's company takes the lead of its sector
        public static readonly string LeadLost = P + "_ledger_lead_lost";     // ...or loses it
        public static readonly string LeadRival = P + "_ledger_lead_rival";   // a rival tycoon's holding takes it
        public static readonly string LeadCity = P + "_ledger_lead_city";     // one of the city's own companies takes it

        public const int LeadBp = 500;       // basis points: a company takes a sector's lead only this far ahead of the holder, so two close ones do not swap it
        public const int SectorsMost = 32;   // sectors the record can hold: the market has 24, and a sector added later has room
        public const int CityMost = 2;       // stories of the city's own companies in one morning; a player's and a rival's are always told

        /// <summary>The least the company passed is worth for its loss to be news, a major rival: the ladder's first figure.</summary>
        public static long Floor => LedgerStories.Ladder[0];

        /// <summary>
        /// The morning's changes of lead from the companies at the last close. A sector not seen yet (every one, for a save
        /// without the record) is set where it stands without a story; afterwards a company that passes the holder by
        /// <see cref="LeadBp"/> has one when both were on the market at the edition before and the holder is worth at least
        /// the <see cref="Floor"/>. A holder that left the market or the sector, or a company new to the market, moves the lead
        /// without one; the city's own companies have <see cref="CityMost"/> a morning. Run before <see cref="LedgerStories.Marks"/>,
        /// whose record says who was on the market. Quiet (a guest reading the host's paper): the record follows, nothing is printed.
        /// </summary>
        public static void Pass(LedgerState s, List<LedgerCompany> companies, bool quiet, Func<double, string> money, Func<int, string> sector,
            List<LedgerPiece> into, ref LedgerStoryCount count)
        {
            bool tell = s.LeadsKept && !quiet && sector != null;
            var largest = new int[SectorsMost];   // the index + 1 of a sector's most valuable company; 0 none
            for (int i = 0; i < companies.Count; i++)
            {
                int at = companies[i].Sector;
                if (InRange(at) && (largest[at] == 0 || Ahead(companies[i], companies[largest[at] - 1])))
                {
                    largest[at] = i + 1;
                }
            }
            int cityTold = 0;
            for (int at = 0; at < SectorsMost; at++)
            {
                if (largest[at] == 0)
                {
                    Drop(s, at);   // no company left in the sector: should one come, it is set where it stands
                    continue;
                }
                LedgerCompany top = companies[largest[at] - 1];
                int holder = HolderOf(s, at);
                if (holder == top.Id)
                {
                    continue;
                }
                int held = holder != 0 ? IndexIn(companies, holder, at) : -1;
                if (held >= 0 && top.Value * 10000.0 < companies[held].Value * (10000.0 + LeadBp))
                {
                    continue;   // the holder keeps the lead until another company is clearly ahead
                }
                if (tell && held >= 0 && companies[held].Value >= Floor && Known(s, top.Id))
                {
                    LedgerCompany beaten = companies[held];
                    string words = sector(at);
                    bool city = !top.Player && !beaten.Player && !top.Rival;
                    if (!string.IsNullOrEmpty(words) && (!city || cityTold < CityMost))
                    {
                        LedgerPiece piece = Story(top, beaten, words, money);
                        if (piece != null)
                        {
                            into.Add(piece);
                            count.Leads++;
                            cityTold += city ? 1 : 0;
                        }
                    }
                }
                Set(s, at, top.Id);
            }
            s.LeadsKept = true;
        }

        /// <summary>
        /// The story of one company passing another as the leader of a sector: a player's company's own when it is the one that
        /// took the lead, the player's loss when it is the one passed, else a rival tycoon's holding's or the city's.
        /// </summary>
        public static LedgerPiece Story(LedgerCompany top, LedgerCompany beaten, string sector, Func<double, string> money)
        {
            if (top.Player)
            {
                return LedgerRules.Piece(LeadOwn, Names(top, beaten, sector, money));
            }
            if (beaten.Player)
            {
                return LedgerRules.Piece(LeadLost, Names(beaten, top, sector, money));   // about the player's company: its name and value come first
            }
            return LedgerRules.Piece(top.Rival ? LeadRival : LeadCity, Names(top, beaten, sector, money));
        }

        /// <summary>
        /// The test's story: a player's company in its sector against the most valuable other company there, else the most valuable
        /// company that has another in its sector against that one. Told as an overtaking whatever the standing, as the test's
        /// takeover is. Null, with no name, when no sector has two companies.
        /// </summary>
        public static LedgerPiece Test(List<LedgerCompany> companies, Func<double, string> money, Func<int, string> sector, out string name)
        {
            name = null;
            if (sector == null)
            {
                return null;
            }
            int pick = -1;
            for (int i = 0; i < companies.Count; i++)
            {
                LedgerCompany c = companies[i];
                if (InRange(c.Sector) && c.Player && Largest(companies, c.Sector, i) >= 0 && (pick < 0 || Ahead(c, companies[pick])))
                {
                    pick = i;
                }
            }
            bool own = pick >= 0;   // a player's company with another in its sector is the test's, whatever its standing
            for (int i = 0; !own && i < companies.Count; i++)
            {
                LedgerCompany c = companies[i];
                if (InRange(c.Sector) && Largest(companies, c.Sector, i) >= 0 && (pick < 0 || Ahead(c, companies[pick])))
                {
                    pick = i;
                }
            }
            if (pick < 0)
            {
                return null;
            }
            LedgerCompany top = companies[pick];
            name = top.Name;
            return Story(top, companies[Largest(companies, top.Sector, pick)], sector(top.Sector), money);
        }

        /// <summary>The leaders forgotten (the stories switched off): switched on again, each sector starts where it stands.</summary>
        public static void Forget(LedgerState s)
        {
            s.LeadsKept = false;
            s.Leads.Clear();
        }

        private static string[] Names(LedgerCompany name, LedgerCompany other, string sector, Func<double, string> money)
        {
            return new[] { "name", name.Name, "value", money(name.Value), "other", other.Name, "other_value", money(other.Value), "sector", sector };
        }

        private static bool InRange(int sector)
        {
            return sector >= 0 && sector < SectorsMost;
        }

        // The more valuable company; at the same value, the lower id: the same on every machine.
        private static bool Ahead(LedgerCompany a, LedgerCompany b)
        {
            return a.Value > b.Value || (a.Value == b.Value && a.Id < b.Id);
        }

        // The index of the most valuable company of a sector but one, or -1 when it has no other.
        private static int Largest(List<LedgerCompany> companies, int sector, int except)
        {
            int best = -1;
            for (int i = 0; i < companies.Count; i++)
            {
                if (i != except && companies[i].Sector == sector && (best < 0 || Ahead(companies[i], companies[best])))
                {
                    best = i;
                }
            }
            return best;
        }

        // The company is still in the sector it led; -1 when it left the market or moved to another sector.
        private static int IndexIn(List<LedgerCompany> companies, int id, int sector)
        {
            for (int i = 0; i < companies.Count; i++)
            {
                if (companies[i].Id == id && companies[i].Sector == sector)
                {
                    return i;
                }
            }
            return -1;
        }

        // True if the record of milestones has the company: it was on the market at the edition before.
        private static bool Known(LedgerState s, int id)
        {
            for (int i = 0; i < s.Marks.Count; i++)
            {
                if (s.Marks[i].Id == id)
                {
                    return true;
                }
            }
            return false;
        }

        private static int HolderOf(LedgerState s, int sector)
        {
            for (int i = 0; i < s.Leads.Count; i++)
            {
                if (s.Leads[i].Sector == sector)
                {
                    return s.Leads[i].Id;
                }
            }
            return 0;
        }

        private static void Set(LedgerState s, int sector, int id)
        {
            for (int i = 0; i < s.Leads.Count; i++)
            {
                if (s.Leads[i].Sector == sector)
                {
                    s.Leads[i] = new LedgerLead { Sector = sector, Id = id };
                    return;
                }
            }
            s.Leads.Add(new LedgerLead { Sector = sector, Id = id });
        }

        private static void Drop(LedgerState s, int sector)
        {
            for (int i = s.Leads.Count - 1; i >= 0; i--)
            {
                if (s.Leads[i].Sector == sector)
                {
                    s.Leads.RemoveAt(i);
                }
            }
        }
    }
}
