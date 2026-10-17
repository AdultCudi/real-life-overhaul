using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Economy
{
    /// <summary>One cleaning shift of a crew member today, as the round sees it. A cleaner with two shifts is two of these.</summary>
    public sealed class RoundCleaner
    {
        public string Id = string.Empty;
        public string Name = string.Empty;
        public int From, To;             // the shift at their base, minutes of today
        public int X, Z;                 // the base's door
        public int Skill, Mood;          // the cleaning skill and the satisfaction, each 0 to 100
        public int FreeFrom;             // busy until this minute with a visit under way when the round is planned again
        public bool Out;                 // ...and then at AtX, AtZ rather than at base
        public int AtX, AtZ;

        // What the plan gave them.
        public int Visits;
        public int Metres;               // walked or driven, the way home included
        public int AwayFrom = -1, AwayTo;    // away from base between these minutes; -1: not away at all
        public int HomeMetres, HomeMinutes;  // the way home from the last visit
    }

    /// <summary>One visit that is due: a fixed hour of a client's, or a client whose floor is under its threshold.</summary>
    public sealed class RoundJob
    {
        public int Client;               // its index in the list of clients
        public int Slot = -1;            // which of its fixed visits; -1: as needed
        public int X, Z;
        public int Force = 1;
        public int Earliest;             // not before this minute
        public int Need = -1;            // hundredths of dirt to take off; -1: not known
        public bool Taken;
        public int Why;                  // why nobody takes it: RoundRules.NoCleaner and its neighbours
    }

    /// <summary>A visit of the round: who, where and when.</summary>
    public struct RoundVisit
    {
        public int Cleaner;              // their index in the list of shifts
        public int Client;
        public int Slot;
        public int Leave;                // the minute they set out for it
        public int Start, End;           // cleaning between these minutes
        public int Rate;                 // hundredths an hour off every tile there
        public int Metres;               // the way to it
    }

    /// <summary>
    /// The day's round, game-free and deterministic: again and again the due visit and shift that can start soonest, so it
    /// optimises how soon each visit begins, not the length of the way. Cost: J x J x C kept distances, once per planning.
    /// </summary>
    public static class RoundRules
    {
        public const int NoCleaner = 1;  // nobody of the crew is on a cleaning shift after this
        public const int TooFar = 2;     // further than anybody goes
        public const int NoTime = 3;     // nobody can get there, clean and be back before their shift ends

        /// <summary>The visits due from now: each fixed hour not passed or made, or, as needed, one now under the threshold with none under way.</summary>
        public static void Jobs(List<CleaningClient> clients, int now, List<RoundJob> into)
        {
            into.Clear();
            for (int i = 0; i < clients.Count; i++)
            {
                CleaningClient c = clients[i];
                if (c.Gone || (!c.Outside && !c.Open))
                {
                    continue;
                }
                if (c.Mode <= ServicesCatalogue.ModeAsNeeded)
                {
                    if (c.Now < c.Threshold && !c.Pending)
                    {
                        into.Add(new RoundJob { Client = i, X = c.X, Z = c.Z, Force = c.Force, Earliest = now, Need = c.Load });
                    }
                    continue;
                }
                int visits = Math.Min(c.Mode, ServicesCatalogue.VisitsMost);
                int share = c.NeedDay > 0 ? c.NeedDay / visits : -1;
                bool first = true;
                for (int k = 0; k < visits; k++)
                {
                    int minute = ServicesCatalogue.Clamp(c.Hours[k], 0, 23) * 60;
                    if ((c.DoneMask & (1 << k)) != 0 || minute + 60 <= now)
                    {
                        continue;
                    }
                    // The next visit finds what is on the floor now; later ones their share of a day's dirt.
                    int need = first ? (c.Load > 0 || share < 0 ? Math.Max(c.Load, share) : share) : share;
                    into.Add(new RoundJob { Client = i, Slot = k, X = c.X, Z = c.Z, Force = c.Force, Earliest = Math.Max(minute, now), Need = need == 0 ? -1 : need });
                    first = false;
                }
            }
        }

        /// <summary>Plans the round from now: a visit for every job somebody can reach, clean and be back from in their shift. Returns how many.</summary>
        public static int Plan(List<RoundCleaner> cleaners, List<RoundJob> jobs, TravelCache cache, bool van, int now, List<RoundVisit> visits)
        {
            int taken = 0;
            for (int c = 0; c < cleaners.Count; c++)
            {
                RoundCleaner who = cleaners[c];
                who.Visits = who.Metres = 0;
                if (!who.Out)
                {
                    who.AtX = who.X;
                    who.AtZ = who.Z;
                }
                who.FreeFrom = Math.Max(who.FreeFrom, Math.Max(who.From, now));
            }
            while (visits.Count < ServicesCatalogue.VisitsDayMost)
            {
                int bestJob = -1, bestCleaner = -1, bestStart = int.MaxValue, bestDue = int.MaxValue, bestWay = int.MaxValue, bestMinutes = 0, bestRate = 0, bestMetres = 0;
                for (int j = 0; j < jobs.Count; j++)
                {
                    RoundJob job = jobs[j];
                    if (job.Taken)
                    {
                        continue;
                    }
                    for (int c = 0; c < cleaners.Count; c++)
                    {
                        RoundCleaner who = cleaners[c];
                        int rate = CleaningRules.Rate(who.Skill, who.Mood, job.Force);
                        if (rate <= 0 || who.FreeFrom >= who.To)
                        {
                            continue;
                        }
                        int metres = cache.Metres(who.AtX, who.AtZ, job.X, job.Z);
                        int way = TravelRules.Minutes(metres, van);
                        if (way > ServicesCatalogue.TravelMost)
                        {
                            continue;
                        }
                        int start = Math.Max(who.FreeFrom + way, job.Earliest);
                        int home = TravelRules.Minutes(cache.Metres(job.X, job.Z, who.X, who.Z), van);
                        int latest = who.To - home;
                        if (start + ServicesCatalogue.VisitLeast > latest)
                        {
                            continue;
                        }
                        if (start < bestStart || (start == bestStart && (job.Earliest < bestDue || (job.Earliest == bestDue && way < bestWay))))
                        {
                            bestJob = j;
                            bestCleaner = c;
                            bestStart = start;
                            bestDue = job.Earliest;
                            bestWay = way;
                            bestMinutes = Math.Min(CleaningRules.Planned(job.Need, rate), latest - start);
                            bestRate = rate;
                            bestMetres = metres;
                        }
                    }
                }
                if (bestJob < 0)
                {
                    break;
                }
                RoundJob got = jobs[bestJob];
                RoundCleaner by = cleaners[bestCleaner];
                got.Taken = true;
                visits.Add(new RoundVisit
                {
                    Cleaner = bestCleaner, Client = got.Client, Slot = got.Slot, Leave = bestStart - bestWay, Start = bestStart, End = bestStart + bestMinutes, Rate = bestRate, Metres = bestMetres
                });
                if (by.AwayFrom < 0)
                {
                    by.AwayFrom = bestStart - bestWay;
                }
                by.FreeFrom = bestStart + bestMinutes;
                by.AtX = got.X;
                by.AtZ = got.Z;
                by.Out = true;
                by.Metres += bestMetres;
                by.Visits++;
                taken++;
            }
            for (int c = 0; c < cleaners.Count; c++)
            {
                RoundCleaner who = cleaners[c];
                if (who.Out)
                {
                    int metres = cache.Metres(who.AtX, who.AtZ, who.X, who.Z);
                    who.Metres += metres;
                    who.HomeMetres = metres;
                    who.HomeMinutes = TravelRules.Minutes(metres, van);
                    who.AwayTo = Math.Min(Math.Max(who.To, who.FreeFrom), who.FreeFrom + who.HomeMinutes);
                    if (who.AwayFrom < 0)
                    {
                        who.AwayFrom = Math.Max(who.From, now);   // out when planned again with nothing more to do: only the way home
                    }
                }
            }
            for (int j = 0; j < jobs.Count; j++)
            {
                if (!jobs[j].Taken)
                {
                    jobs[j].Why = Why(cleaners, jobs[j], cache, van);
                }
            }
            return taken;
        }

        // Why nobody takes a visit: the first reason that holds for everybody.
        private static int Why(List<RoundCleaner> cleaners, RoundJob job, TravelCache cache, bool van)
        {
            bool anyShift = false, anyNear = false;
            for (int c = 0; c < cleaners.Count; c++)
            {
                RoundCleaner who = cleaners[c];
                if (who.FreeFrom >= who.To || CleaningRules.Rate(who.Skill, who.Mood, job.Force) <= 0)
                {
                    continue;
                }
                anyShift = true;
                anyNear |= TravelRules.Minutes(cache.Metres(who.X, who.Z, job.X, job.Z), van) <= ServicesCatalogue.TravelMost;
            }
            return !anyShift ? NoCleaner : !anyNear ? TooFar : NoTime;
        }

        /// <summary>The minutes two spans of time share.</summary>
        public static int Overlap(int from, int to, int otherFrom, int otherTo) => Math.Max(0, Math.Min(to, otherTo) - Math.Max(from, otherFrom));

        /// <summary>The minutes of a visit to clean at the hour beginning at hourStart: all of it up to that hour's end not cleaned yet.</summary>
        public static int Due(int start, int end, int done, int hourStart) => Math.Max(0, Overlap(start, end, 0, hourStart + 60) - Math.Max(0, done));

        /// <summary>The minutes of the hour beginning at hourStart that a cleaner is away from base.</summary>
        public static int Away(RoundCleaner who, int hourStart) => who.AwayFrom < 0 ? 0 : Overlap(who.AwayFrom, who.AwayTo, hourStart, hourStart + 60);
    }
}
