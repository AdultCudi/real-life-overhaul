using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Schemes
{
    /// <summary>
    /// The schemes by rule: what an act adds to its trail, how the trail fades, the line an inquiry opens at, what a file
    /// holds against the player and which charge it becomes. Every draw is a hash of the save's seed and what it is about,
    /// so asking twice, or loading and asking again, gives the same answer. Game-free: the day and every figure of the
    /// game come in as arguments. Its parts are the people (People), the money (Money), the day (Day) and the rivals (Rivals).
    /// </summary>
    public static partial class SchemeRules
    {
        // What each draw is about: a number of its own, so that no two draws share their luck.
        private const int DrawLine = 11, DrawSpot = 12, DrawTemper = 13, DrawTalk = 14, DrawDiligence = 15, DrawBent = 16, DrawRival = 17, DrawRivalKind = 18;
        private const int DrawRivalSize = 19, DrawFundWho = 20, DrawFundSham = 21, DrawDoubt = 22, DrawSeat = 23, DrawClub = 24;

        /// <summary>
        /// The game's name for a playthrough, for the schemes' seed: the city's seed and the playthrough's own id (the character's unique id).
        /// The city's seed alone is the same for every playthrough on one city, so a second save would meet the same temper draws and the same
        /// lines and could learn the answers from the first. The same save always gives the same text, on every load and every machine that
        /// holds that save. With no city seed the id alone stands, and with neither a fixed word.
        /// </summary>
        public static string PlaythroughOf(string citySeed, string playthroughId)
        {
            bool city = !string.IsNullOrEmpty(citySeed), own = !string.IsNullOrEmpty(playthroughId);
            return city && own ? citySeed + "|" + playthroughId : city ? citySeed : own ? playthroughId : "city";
        }

        /// <summary>A save's seed for the schemes, from the game's own name for the playthrough: never 0, and the same on every load.</summary>
        public static int SeedOf(string playthrough)
        {
            int seed = JusticeLaw.Hash(string.IsNullOrEmpty(playthrough) ? "city" : playthrough);
            return seed == 0 ? 1 : seed;
        }

        /// <summary>0 to 999 from the seed and what the draw is about: the same inputs give the same number on every machine.</summary>
        public static int Roll(int seed, int purpose, int a, int b)
        {
            unchecked
            {
                uint x = (uint)seed * 2654435761u ^ (uint)purpose * 40503u;
                x = (x ^ (x >> 15)) * 2246822519u + (uint)a * 3266489917u;
                x = (x ^ (x >> 13)) * 668265263u + (uint)b * 374761393u;
                x ^= x >> 16;
                x *= 2246822519u;
                x ^= x >> 13;
                return (int)(x % 1000u);
            }
        }

        /// <summary>A text as a number (FNV-1a), the same on every machine: a person's id, a place.</summary>
        public static int Text(string text) => JusticeLaw.Hash(text ?? string.Empty);

        // ------------------------------------------------------------------------------------------------- the trail

        /// <summary>Earlier acts of a kind within the days a pattern is read over, as far as they are counted.</summary>
        public static int Recent(SchemeState s, SchemeKind kind, int day)
        {
            int recent = 0;
            for (int i = 0; i < s.Acts.Count; i++)
            {
                SchemeAct a = s.Acts[i];
                recent += a.Kind == kind && day - a.Day <= SchemeCatalogue.RepeatDays && day >= a.Day ? 1 : 0;
            }
            return Math.Min(SchemeCatalogue.RepeatMost, recent);
        }

        /// <summary>Older acts of a kind still on file, past the days a pattern is read over, as far as they are counted: found ones are dealt with and count no more.</summary>
        public static int Older(SchemeState s, SchemeKind kind, int day)
        {
            int older = 0;
            for (int i = 0; i < s.Acts.Count; i++)
            {
                older += OnFile(s.Acts[i], kind, day) && day - s.Acts[i].Day > SchemeCatalogue.RepeatDays ? 1 : 0;
            }
            return Math.Min(SchemeCatalogue.OnFileMost, older);
        }

        /// <summary>
        /// What an act adds to its kind's heat: its kind, its size, the pattern it is part of (the recent acts, and the
        /// older ones still on file), who knows, and its paper trail.
        /// </summary>
        public static int HeatOf(SchemeKind kind, int size, int recent, int older, int knows, bool paper)
        {
            int heat = SchemeCatalogue.HeatBase[(int)kind] + SchemeCatalogue.Clamp(size, 0, SchemeCatalogue.Large) * SchemeCatalogue.HeatSize / 1000
                       + SchemeCatalogue.Clamp(recent, 0, SchemeCatalogue.RepeatMost) * SchemeCatalogue.HeatRepeat
                       + SchemeCatalogue.Clamp(older, 0, SchemeCatalogue.OnFileMost) * SchemeCatalogue.HeatOnFile + Math.Max(0, knows) * SchemeCatalogue.HeatKnows
                       + (paper ? SchemeCatalogue.HeatPaper : 0);
            return Math.Min(SchemeCatalogue.HeatMost, heat);
        }

        /// <summary>
        /// How often a first act of its kind and size is looked into, per cent: the share of saves whose line it passes on
        /// its own, and of the rest those a routine check carries over it. What a page says before the player commits; an
        /// act with others on file is looked into more often than this.
        /// </summary>
        public static int FirstChance(SchemeKind kind, int size, int knows, bool paper)
        {
            int heat = HeatOf(kind, size, 0, 0, knows, paper), plain = Passes(heat), spotted = Passes(heat + SchemeCatalogue.HeatSpotted);
            return (plain + (spotted - plain) * SchemeCatalogue.SpotPermille / 1000 + 5) / 10;
        }

        // The share of saves, thousandths, whose line a heat passes: the line is drawn evenly from its least to its most.
        private static int Passes(int heat)
        {
            return SchemeCatalogue.Clamp((heat - SchemeCatalogue.OpenLeast + 1) * 1000 / SchemeCatalogue.OpenSpread, 0, 1000);
        }

        /// <summary>Whether an act is spotted by a routine check whatever its size: fixed by the seed and the act's number.</summary>
        public static bool Spotted(int seed, SchemeKind kind, int number)
        {
            return Roll(seed, DrawSpot, (int)kind, number) < SchemeCatalogue.SpotPermille;
        }

        /// <summary>Heat on a trail, and a share of it on every other: bounded.</summary>
        public static void AddHeat(SchemeState s, SchemeKind kind, int heat)
        {
            if (heat <= 0)
            {
                return;
            }
            for (int k = 0; k < s.Heat.Length; k++)
            {
                int add = k == (int)kind ? heat : heat * SchemeCatalogue.HeatSpill / 1000;
                s.Heat[k] = Math.Min(SchemeCatalogue.HeatMost, s.Heat[k] + add);
            }
        }

        /// <summary>
        /// An act goes on its trail: its heat, the pattern it is part of and whether a routine check spots it. The one place
        /// an act is recorded, so the same press always leaves the same trail.
        /// </summary>
        public static SchemeAct Record(SchemeState s, SchemeKind kind, int day, long gain, int size, int knows, bool paper, string place, int reference)
        {
            int recent = Recent(s, kind, day), older = Older(s, kind, day);
            var act = new SchemeAct
            {
                Kind = kind, Day = day, Gain = Math.Max(0L, gain), Size = SchemeCatalogue.Clamp(size, 0, SchemeCatalogue.Large), Knows = Math.Max(0, knows), Paper = paper,
                Place = place ?? string.Empty, Ref = reference
            };
            Keep(s, act);
            int heat = HeatOf(kind, act.Size, recent, older, act.Knows, paper);
            if (Spotted(s.Seed, kind, s.Acted))
            {
                heat += SchemeCatalogue.HeatSpotted;
            }
            s.Acted++;
            AddHeat(s, kind, heat);
            return act;
        }

        // Room for one more of its kind: the oldest act an inquiry has already found goes first, else the oldest.
        private static void Keep(SchemeState s, SchemeAct act)
        {
            int count = 0, oldest = -1, oldestFound = -1;
            for (int i = 0; i < s.Acts.Count; i++)
            {
                if (s.Acts[i].Kind != act.Kind)
                {
                    continue;
                }
                count++;
                oldest = oldest < 0 ? i : oldest;
                oldestFound = oldestFound < 0 && s.Acts[i].Inquiry != 0 ? i : oldestFound;
            }
            if (count >= SchemeCatalogue.ActsMost)
            {
                s.Acts.RemoveAt(oldestFound >= 0 ? oldestFound : oldest);
            }
            s.Acts.Add(act);
        }

        /// <summary>The line an inquiry into a kind opens at: drawn from the seed for the kind, the period the day is in and the inquiries so far.</summary>
        public static int Line(int seed, SchemeKind kind, int day, int opened)
        {
            int period = Math.Max(0, day) / SchemeCatalogue.OpenPeriodDays;
            return SchemeCatalogue.OpenLeast + Roll(seed, DrawLine, (int)kind * 31 + opened, period) * SchemeCatalogue.OpenSpread / 1000;
        }

        /// <summary>How a trail stands in a word: 0 calm, 1 talked about, 2 watched. An open inquiry is said apart.</summary>
        public static int Band(int heat)
        {
            return heat < SchemeCatalogue.OpenLeast / 2 ? 0 : heat < SchemeCatalogue.OpenLeast ? 1 : 2;
        }

        /// <summary>True for an act an inquiry of this day could still find: of the kind, not found before, and not older than a file reaches.</summary>
        public static bool OnFile(SchemeAct a, SchemeKind kind, int day)
        {
            return a.Kind == kind && a.Inquiry == 0 && day - a.Day <= SchemeCatalogue.TrailDays;
        }

        /// <summary>What the acts of a kind on file brought in all, whole money: what decides between a misdemeanour and a felony.</summary>
        public static long Brought(SchemeState s, SchemeKind kind, int day)
        {
            long sum = 0;
            for (int i = 0; i < s.Acts.Count; i++)
            {
                SchemeAct a = s.Acts[i];
                sum += OnFile(a, kind, day) ? Math.Max(a.Gain, a.Paid) : 0L;
            }
            return sum;
        }

        /// <summary>What the acts of a kind on file still owe: what paying it back costs today, and what an inquiry would take.</summary>
        public static long Owed(SchemeState s, SchemeKind kind, int day)
        {
            long sum = 0;
            for (int i = 0; i < s.Acts.Count; i++)
            {
                sum += OnFile(s.Acts[i], kind, day) ? s.Acts[i].Owed : 0L;
            }
            return sum;
        }

        /// <summary>How many acts of a kind are on file.</summary>
        public static int Count(SchemeState s, SchemeKind kind, int day)
        {
            int count = 0;
            for (int i = 0; i < s.Acts.Count; i++)
            {
                count += OnFile(s.Acts[i], kind, day) ? 1 : 0;
            }
            return count;
        }

        /// <summary>A file of this much is a felony: ten small frauds are one large one.</summary>
        public static bool Felony(SchemeState s, SchemeKind kind, int day, int level)
        {
            return Brought(s, kind, day) >= SchemeCatalogue.AtPrices(SchemeCatalogue.FelonyFrom, level);
        }

        /// <summary>
        /// The charge a file becomes (Justice's own ids): by its kind, by how much it is about, and the lesser one for
        /// somebody who told the investigators everything.
        /// </summary>
        public static string ChargeOf(SchemeState s, SchemeKind kind, int day, int level, bool coop)
        {
            bool felony = Felony(s, kind, day, level) && !coop;
            switch (kind)
            {
                case SchemeKind.Expense:
                    return JusticeLaw.TaxEvasion;
                case SchemeKind.Wash:
                    return felony ? JusticeLaw.MoneyLaundering : JusticeLaw.FalseAccounting;
                case SchemeKind.Graft:
                    return Perverted(s, day) && !coop ? JusticeLaw.PervertingJustice : JusticeLaw.Bribery;
                default:
                    return felony ? JusticeLaw.Fraud : JusticeLaw.FalseAccounting;
            }
        }

        // Whether the graft on file reached an investigator or a judge: that is more than bribery.
        private static bool Perverted(SchemeState s, int day)
        {
            for (int i = 0; i < s.Acts.Count; i++)
            {
                SchemeAct a = s.Acts[i];
                if (OnFile(a, SchemeKind.Graft, day) && (a.Ref == (int)Favour.Investigator || a.Ref == (int)Favour.Judge))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// What the court makes of a file with nobody's answers in it: guilty when the case against is stronger than the
        /// court's doubt, by Justice's own sum (JusticeLaw.CaseAgainst) and a doubt drawn from the seed. A rival's trial
        /// is decided so, and the checks' player's.
        /// </summary>
        public static bool Tried(int seed, int number, int evidence, int priors, int counsel)
        {
            int against = JusticeLaw.CaseAgainst(evidence / 1000f, priors, counsel / 1000f, 0);
            return Roll(seed, DrawDoubt, number, priors) < against;
        }

        /// <summary>The fine a court sets for a charge after a trial, at a price level and bounded against the cash in hand as every fine is.</summary>
        public static int FineOf(string charge, int priors, float cash)
        {
            ChargeDef def = JusticeLaw.Find(charge);
            if (def == null)
            {
                return 0;
            }
            Fined.Clear();
            Fined.Add(def);
            return JusticeLaw.Sentence(Fined, 1f, 0f, priors, false, JusticeLaw.PrisonCapDefault, false, cash, JusticeLaw.TrialFineFactor).Fine;
        }

        private static readonly List<ChargeDef> Fined = new List<ChargeDef>(1);
    }
}
