using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Economy
{
    /// <summary>
    /// What the cleaning contracts keep beyond one offer: rival cleaning firms that bid for the same contract and win some, the named crews that
    /// serve a contract, the fees contracted a month over twelve months, and a business's worth by day. Game-free and deterministic: every draw
    /// is the save's seed with the day and the place.
    /// </summary>
    public static partial class ContractRules
    {
        private const int PurposeRivals = 7101, PurposeFirm = 7102, PurposePct = 7103, PurposeSkill = 7104;

        /// <summary>The rivals that bid for an offer as it opens: none to three, each a firm, a share of the asking fee and a crew's skill.</summary>
        public static int Bid(ServicesState s, CleaningOffer o, int day)
        {
            o.Rivals.Clear();
            int place = ServicesCatalogue.Number(o.Place);
            int count = ServicesCatalogue.Roll(s.Seed, day, PurposeRivals, place, ServicesCatalogue.RivalBidsMost + 1);
            int first = ServicesCatalogue.Roll(s.Seed, day, PurposeFirm, place, ServicesCatalogue.RivalFirms.Length);
            for (int i = 0; i < count; i++)
            {
                o.Rivals.Add(new RivalBid
                {
                    Firm = (first + i * 3) % ServicesCatalogue.RivalFirms.Length,
                    Pct = 82 + ServicesCatalogue.Roll(s.Seed, day, PurposePct, place + i, 29),
                    Skill = 40 + ServicesCatalogue.Roll(s.Seed, day, PurposeSkill, place + i, 51)
                });
            }
            return count;
        }

        /// <summary>How a client weighs a bid: a point of the crew's skill counts for twice a point of the fee, so a dearer firm that cleans better wins.</summary>
        public static int Score(int pct, int skill) => skill * 2 - pct;

        /// <summary>The rival that wins the contract against the crew at the asking fee, or -1: the best score takes it, a tie is the player's.</summary>
        public static int Winner(CleaningOffer o, int crewSkill)
        {
            int best = -1, bestScore = Score(100, crewSkill);
            for (int i = 0; i < o.Rivals.Count; i++)
            {
                int score = Score(o.Rivals[i].Pct, o.Rivals[i].Skill);
                if (score > bestScore)
                {
                    best = i;
                    bestScore = score;
                }
            }
            return best;
        }

        /// <summary>
        /// Takes away the offers whose days have run out. A rival that wins one takes it (its name and firm go to <paramref name="lost"/>); the rest lapse.
        /// Returns how many went.
        /// </summary>
        public static int Close(ServicesState s, int day, int crewSkill, List<KeyValuePair<string, int>> lost)
        {
            int gone = 0;
            for (int i = s.Offers.Count - 1; i >= 0; i--)
            {
                CleaningOffer o = s.Offers[i];
                if (day <= o.Until)
                {
                    continue;
                }
                int rival = Winner(o, crewSkill);
                if (rival >= 0)
                {
                    s.RivalWon++;
                    lost?.Add(new KeyValuePair<string, int>(o.Name, o.Rivals[rival].Firm));
                }
                s.Offers.RemoveAt(i);
                gone++;
            }
            return gone;
        }

        // ------------------------------------------------------------------------------------------------- named crews

        /// <summary>A member of the crew goes into a named crew, or out of every one (a group below nothing).</summary>
        public static void Place(ServicesState s, string id, int group)
        {
            for (int i = s.Places.Count - 1; i >= 0; i--)
            {
                if (string.Equals(s.Places[i].Id, id, StringComparison.Ordinal))
                {
                    s.Places.RemoveAt(i);
                }
            }
            if (group >= 0 && group < ServicesCatalogue.CrewGroups && s.Places.Count < ServicesCatalogue.CrewMost && s.InCrew(id))
            {
                s.Places.Add(new CrewPlace { Id = id, Group = group });
            }
        }

        /// <summary>Somebody who left the crew leaves their named crew too, and a contract kept by a crew nobody stands in is kept by whoever is free.</summary>
        public static void Tidy(ServicesState s)
        {
            for (int i = s.Places.Count - 1; i >= 0; i--)
            {
                if (!s.InCrew(s.Places[i].Id))
                {
                    s.Places.RemoveAt(i);
                }
            }
            for (int c = 0; c < s.Clients.Count; c++)
            {
                bool stands = false;
                for (int i = 0; i < s.Places.Count; i++)
                {
                    stands |= s.Places[i].Group == s.Clients[c].Crew;
                }
                s.Clients[c].Crew = stands ? s.Clients[c].Crew : -1;
            }
        }

        /// <summary>The lead of a named crew: its best cleaner, the first of them on a tie; null for a crew nobody stands in.</summary>
        public static string Lead(ServicesState s, int group, Func<string, int> skill)
        {
            string lead = null;
            int best = -1;
            for (int i = 0; i < s.Places.Count; i++)
            {
                int have = s.Places[i].Group == group ? skill(s.Places[i].Id) : -1;
                if (have > best)
                {
                    best = have;
                    lead = s.Places[i].Id;
                }
            }
            return lead;
        }

        // ------------------------------------------------------------------------------------------------- the fees contracted a month

        /// <summary>What the running contracts' weekly fees come to over a month of days.</summary>
        public static int MonthFees(ServicesState s)
        {
            long weekly = 0;
            for (int i = 0; i < s.Clients.Count; i++)
            {
                weekly += s.Clients[i].Outside ? s.Clients[i].Fee : 0;
            }
            return (int)Math.Min(int.MaxValue / 4, weekly * ServicesCatalogue.MonthDays / ServicesCatalogue.ContractWeekDays);
        }

        /// <summary>Notes the figure once, on the first day of a new month. A month the service was off for takes the figure of the month it comes back in. True when one was added.</summary>
        public static bool NoteMonth(ServicesState s, int day)
        {
            int month = day / ServicesCatalogue.MonthDays;
            if (month <= s.FeeMonth)
            {
                return false;
            }
            Array.Copy(s.FeeMonths, 1, s.FeeMonths, 0, s.FeeMonths.Length - 1);
            s.FeeMonths[s.FeeMonths.Length - 1] = MonthFees(s);
            s.FeeMonth = month;
            s.FeeNoted = Math.Min(ServicesCatalogue.FeeMonths, s.FeeNoted + 1);
            return true;
        }

        /// <summary>The months noted so far, oldest first: fewer than the ring holds before it has filled.</summary>
        public static int[] MonthSeries(ServicesState s)
        {
            var series = new int[Math.Max(0, Math.Min(s.FeeNoted, ServicesCatalogue.FeeMonths))];
            Array.Copy(s.FeeMonths, s.FeeMonths.Length - series.Length, series, 0, series.Length);
            return series;
        }

        // ------------------------------------------------------------------------------------------------- a business's worth by day

        /// <summary>Notes a business's worth for a day, in the slot the day falls in.</summary>
        public static void NoteWorth(ServicesState s, string place, int day, int value)
        {
            WorthRing ring = null;
            for (int i = 0; i < s.Worths.Count && ring == null; i++)
            {
                ring = string.Equals(s.Worths[i].Place, place, StringComparison.Ordinal) ? s.Worths[i] : null;
            }
            if (ring == null)
            {
                if (s.Worths.Count >= ServicesCatalogue.ListingsMost)
                {
                    s.Worths.RemoveAt(0);
                }
                ring = new WorthRing { Place = place };
                s.Worths.Add(ring);
            }
            int slot = ((day % ServicesCatalogue.WorthDays) + ServicesCatalogue.WorthDays) % ServicesCatalogue.WorthDays;
            ring.Value[slot] = Math.Max(0, value);
            ring.Day[slot] = day;
        }

        /// <summary>The days of a business's worth that are kept, oldest first, up to today; empty where none was noted.</summary>
        public static int[] WorthSeries(ServicesState s, string place, int today)
        {
            for (int i = 0; i < s.Worths.Count; i++)
            {
                if (!string.Equals(s.Worths[i].Place, place, StringComparison.Ordinal))
                {
                    continue;
                }
                var list = new List<int>(ServicesCatalogue.WorthDays);
                for (int d = today - ServicesCatalogue.WorthDays + 1; d <= today; d++)
                {
                    int slot = ((d % ServicesCatalogue.WorthDays) + ServicesCatalogue.WorthDays) % ServicesCatalogue.WorthDays;
                    if (d > 0 && s.Worths[i].Day[slot] == d)
                    {
                        list.Add(s.Worths[i].Value[slot]);
                    }
                }
                return list.ToArray();
            }
            return new int[0];
        }

        /// <summary>The change from the first kept day to the last in tenths of a per cent; 0 with fewer than two days.</summary>
        public static int Change(int[] series)
        {
            return series.Length < 2 || series[0] <= 0 ? 0 : (int)Math.Round((series[series.Length - 1] - series[0]) * 1000.0 / series[0]);
        }
    }
}
