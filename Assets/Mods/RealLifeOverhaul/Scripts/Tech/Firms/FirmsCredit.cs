using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Tech
{
    /// <summary>What a firm that was made again is owed, for the log and the notice.</summary>
    public sealed class CreditLine
    {
        public string Place = string.Empty, Name = string.Empty;
        public long ModelCents, GameCents;
        public int Models, Games;
        public long Cents => ModelCents + GameCents;
    }

    /// <summary>The credit the second pass of the repair pays: one line for each firm it found made again, and every step told for Player.log.</summary>
    public sealed class CreditReport
    {
        public readonly List<CreditLine> Lines = new List<CreditLine>(4);
        public readonly List<string> Steps = new List<string>(8);

        public long Cents
        {
            get
            {
                long sum = 0;
                for (int i = 0; i < Lines.Count; i++)
                {
                    sum += Lines[i].Cents;
                }
                return sum;
            }
        }
    }

    /// <summary>
    /// An update closed a tech firm's record and its products whenever its business was not read, so the player trained their AI model (and
    /// built their game) again from nothing, and paid the rented compute again. The first pass of the repair (<see cref="FirmsRepair"/>) gave a
    /// firm its founding day back and paid what the bank statement still showed; for a save hit before the copy existed it could pay nothing.
    /// This pass pays back what the firm's models and games demonstrably cost since it was made again, worked out from the save and the
    /// rules' own prices and never from the statement: each generation a model has been trained is the AI compute of
    /// <see cref="InfraRules.TrainNeed"/> at the rented price (the dearest the rules let it cost: the player's own racks give it cheaper), plus the
    /// training it holds towards the next; a game in the studio is the fees and spending the studio books in cash. A model started fresh for its own
    /// sake cannot be told from one trained again, inside a firm that was made again, so the whole retraining of such a firm is paid. Game-free.
    /// </summary>
    public static class FirmsCredit
    {
        /// <summary>The AI compute units a model has been trained for: every generation built (<see cref="InfraRules.TrainNeed"/> of each feature) and the training banked towards the next.</summary>
        public static long Units(int features, long compute)
        {
            long units = Math.Max(0L, compute);
            for (int f = 0; f < Math.Max(0, features); f++)
            {
                units += InfraRules.TrainNeed(f);
            }
            return units;
        }

        /// <summary>What training so many units cost at the rented price, cents at the price level <paramref name="level"/> (thousandths; 1000 is the start's).</summary>
        public static long UnitsCents(long units, int level) => InfraRules.RentCost(units) * Math.Max(0, level) / 1000;

        /// <summary>What a model's training cost: its generations and its banked training. Nothing for a withdrawn model or one that is no model.</summary>
        public static long ModelCents(Product p, int level) => InfraRules.Trains(p) ? UnitsCents(Units(p.Features, p.Compute), level) : 0L;

        /// <summary>
        /// What a game in the studio cost in cash that a game made again costs again: a licensed engine's fee, the trailer and the expo demo if they were done,
        /// the console certification once passed, and the marketing campaign spent. The team's pay is the game's own wages and is not counted.
        /// </summary>
        public static long GameCents(GameDev g, int level)
        {
            if (g == null)
            {
                return 0L;
            }
            int s = TalentCatalogue.Clamp(g.Scope, 0, 2);
            long cents = g.Engine == (int)GameEngine.Licensed ? StudioCatalogue.EngineFee[s] : 0L;
            cents += g.LastTrailer >= g.Started ? StudioCatalogue.TrailerCost[s] : 0L;
            cents += g.LastExpo >= g.Started ? StudioCatalogue.ExpoCost[s] : 0L;
            cents += g.CertUntil > 0 && g.On(Platform.Console) ? StudioCatalogue.CertificationCost[s] : 0L;
            cents += g.CampaignSpent;
            return cents * Math.Max(0, level) / 1000;
        }

        /// <summary>The first day any professional of the place joined, with how many joined at all; <see cref="int.MaxValue"/> and 0 for none.</summary>
        public static int FirstJoined(TalentFile talent, string place, out int count)
        {
            int first = int.MaxValue;
            count = 0;
            for (int i = 0; talent != null && i < talent.People.Count; i++)
            {
                Profile p = talent.People[i];
                if (p.Place == place && p.Joined > 0)
                {
                    count++;
                    first = Math.Min(first, p.Joined);
                }
            }
            return first;
        }

        /// <summary>
        /// Why a firm is not taken for one made again, in words for the log: what its professionals' joining days say against its founding day.
        /// </summary>
        public static string WhyNot(TalentFile talent, string place, int founded)
        {
            if (talent == null)
            {
                return "the people part of the mod is off, so there is no staff to show it";
            }
            int all = 0, before = 0;
            for (int i = 0; i < talent.People.Count; i++)
            {
                Profile p = talent.People[i];
                if (p.Place == place && p.Joined > 0)
                {
                    all++;
                    before += p.Joined < founded - FirmsRepair.Margin ? 1 : 0;
                }
            }
            if (all == 0)
            {
                return "it has no professional on record with a joining day";
            }
            return before + " of its " + all + " professional(s) joined more than " + FirmsRepair.Margin + " days before its founding day (day " + founded
                   + "); two, and at least half of them, are needed to show the record was made again";
        }

        /// <summary>
        /// Looks at every firm of <paramref name="now"/> and credits those found made again. <paramref name="afterFirst"/>: the first pass already ran (the firm's
        /// founding day was given back, so the sign is that it equals the day its first professional joined, and the first pass found a firm at all:
        /// <paramref name="markFirms"/>); otherwise the record's own founding day is the sign, as the first pass reads it. Call it BEFORE the first pass
        /// changes anything, so what the firm trained since it was made again is read as it stood. <paramref name="copy"/> is the copy of an earlier read.
        /// </summary>
        public static CreditReport Review(FirmsFile now, StudioFile studio, TalentFile talent, ICollection<string> standing, int level, bool afterFirst,
            int markFirms, int markProducts, FirmsFile copy)
        {
            var rep = new CreditReport();
            if (afterFirst && markFirms <= 0)
            {
                rep.Steps.Add("credit: the first repair found no firm made again in this save; nothing to credit");
            }
            for (int i = 0; now != null && i < now.Firms.Count && !(afterFirst && markFirms <= 0); i++)
            {
                Firm f = now.Firms[i];
                string at = "credit: the firm at " + f.Place + (f.Name.Length > 0 ? " (" + f.Name + ")" : string.Empty) + " ";
                if (standing != null && !standing.Contains(f.Place))
                {
                    rep.Steps.Add(at + "not credited: its business was not read today, so it is held as it is");
                    continue;
                }
                int first = FirstJoined(talent, f.Place, out int staff);
                string sign;
                if (!afterFirst)
                {
                    if (FirmsRepair.StaffBefore(talent, f.Place, f.Founded) == int.MaxValue)
                    {
                        rep.Steps.Add(at + "not credited: " + WhyNot(talent, f.Place, f.Founded));
                        continue;
                    }
                    sign = "its professionals joined before its record's founding day (day " + f.Founded + ")";
                }
                else
                {
                    if (staff < 2 || first != f.Founded)
                    {
                        rep.Steps.Add(at + "not credited: its founding day (day " + f.Founded + ") is not the day its first professional joined (" + (staff == 0 ? "none on record" : "day " + first)
                                      + (staff < 2 ? ", " + staff + " professional(s)" : string.Empty) + "), which is how the first repair left a firm it put right");
                        continue;
                    }
                    if (markProducts > 0 && copy != null && copy.FirmAt(f.Place) != null)
                    {
                        rep.Steps.Add(at + "not credited: the first repair put its products back from the copy and paid the rent the statement showed");
                        continue;
                    }
                    sign = "its founding day is the day its first professional joined (day " + first + "), as the first repair left a record it found made again";
                }
                int since = afterFirst ? 0 : f.Founded;
                var line = new CreditLine { Place = f.Place, Name = f.Name };
                for (int p = 0; p < now.Products.Count; p++)
                {
                    Product pr = now.Products[p];
                    long cents = pr.Place == f.Place ? ModelCents(pr, level) : 0L;
                    if (cents > 0)
                    {
                        line.Models++;
                        line.ModelCents += cents;
                        rep.Steps.Add("  model " + pr.Name + ": " + pr.Features + " generation(s) built and " + Math.Max(0L, pr.Compute) + " unit-days banked = " + Units(pr.Features, pr.Compute)
                                      + " AI compute unit-days at the rented price, " + cents + " cents");
                    }
                }
                for (int g = 0; studio != null && g < studio.Games.Count; g++)
                {
                    GameDev game = studio.Games[g];
                    Product pr = game.Place == f.Place ? now.ProductOf(game.Id) : null;
                    if (pr == null || pr.Stage >= 2 || game.Started < since)
                    {
                        continue;
                    }
                    long cents = GameCents(game, level);
                    if (cents > 0)
                    {
                        line.Games++;
                        line.GameCents += cents;
                        rep.Steps.Add("  game " + pr.Name + ": engine fee, trailer, expo, certification and campaign booked in cash, " + cents + " cents");
                    }
                }
                if (line.Cents <= 0)
                {
                    rep.Steps.Add(at + "is flagged (" + sign + ") but holds no AI model that has been trained and no game with costs: nothing to credit");
                    continue;
                }
                rep.Steps.Add(at + "is flagged (" + sign + "): " + line.Cents + " cents for " + line.Models + " model(s) and " + line.Games + " game(s)");
                rep.Lines.Add(line);
            }
            return rep;
        }

        /// <summary>Takes off each firm's line what the first pass already paid back at it (the rent it found in the statement), so nothing is paid twice.</summary>
        public static void Net(CreditReport credit, IDictionary<string, long> paid)
        {
            for (int i = credit.Lines.Count - 1; i >= 0; i--)
            {
                CreditLine l = credit.Lines[i];
                if (paid != null && paid.TryGetValue(l.Place, out long done) && done > 0)
                {
                    long was = l.Cents;
                    long cut = Math.Min(done, l.ModelCents);
                    l.ModelCents -= cut;
                    credit.Steps.Add("credit: the firm at " + l.Place + ": " + done + " cents already paid back by the first repair are taken off " + was + ", leaving " + l.Cents);
                }
                if (l.Cents <= 0)
                {
                    credit.Lines.RemoveAt(i);
                }
            }
        }

        /// <summary>The names of the firms credited, for the notice: at most <paramref name="room"/> characters each, cut with no dots.</summary>
        public static string Named(CreditLine l, int room = 40)
        {
            string n = l.Name.Length > 0 ? l.Name : l.Place;
            return n.Length <= room ? n : n.Substring(0, room).TrimEnd();
        }

    }
}
