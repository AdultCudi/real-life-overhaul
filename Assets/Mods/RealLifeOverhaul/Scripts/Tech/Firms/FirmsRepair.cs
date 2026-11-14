using System;
using System.Collections.Generic;
using System.Globalization;

namespace RealLifeOverhaul.Tech
{
    /// <summary>What the one-time repair did, for its log, its notice and its mark in the save.</summary>
    public sealed class RepairReport
    {
        public int Firms;           // firms whose record was found rebuilt and put right (founding day at least)
        public int Products;        // products put back, or put back on sale
        public long RefundCents;    // compute rent booked after the loss, for the generation the copy shows was already trained
        public readonly List<string> Steps = new List<string>(8);

        public bool Any => Firms > 0 || Products > 0 || RefundCents > 0;
    }

    /// <summary>
    /// The one-time repair of a save an update had cost its tech firms: a firm whose record was made again (its business stood, its
    /// professionals joined before the record's founding day) gets its founding day back, and where the copy the mod keeps of the
    /// last good read still holds the firm, its figures and every product with its launched state come back; the compute rent
    /// booked since, for a model the copy shows was trained, is paid back. A save that was never hit has no firm like that and
    /// changes by nothing. Game-free; the caller asks the ledger and books the money.
    /// </summary>
    public static class FirmsRepair
    {
        public const string Key = "reallife:tech_repair";

        /// <summary>Days a record's founding may follow the first professional's joining before it counts as made again: the record is made at the day's read.</summary>
        public const int Margin = 3;

        public static bool Done(string line) => !string.IsNullOrEmpty(line) && line.StartsWith("v=1", StringComparison.Ordinal);

        public static string Mark(RepairReport r, int day) =>
            "v=1|" + day.ToString(CultureInfo.InvariantCulture) + "|" + r.Firms.ToString(CultureInfo.InvariantCulture) + "|" + r.Products.ToString(CultureInfo.InvariantCulture)
            + "|" + r.RefundCents.ToString(CultureInfo.InvariantCulture);

        /// <summary>
        /// The first day a professional of the place joined, when most of them (two at least) joined before <paramref name="founded"/> less the margin: a firm's
        /// record is made after its business, so a staff that came before it says the record was made again. <see cref="int.MaxValue"/> otherwise (one
        /// professional moved in from another business of the player's says nothing).
        /// </summary>
        public static int StaffBefore(TalentFile talent, string place, int founded)
        {
            int first = int.MaxValue, before = 0, all = 0;
            for (int i = 0; talent != null && i < talent.People.Count; i++)
            {
                Profile p = talent.People[i];
                if (p.Place != place || p.Joined <= 0)
                {
                    continue;
                }
                all++;
                if (p.Joined < founded - Margin)
                {
                    before++;
                    first = Math.Min(first, p.Joined);
                }
            }
            return before >= 2 && before * 2 >= all ? first : int.MaxValue;
        }

        /// <summary>
        /// Puts right every firm of <paramref name="now"/> (the firms whose businesses stand) that was made again. <paramref name="rentSince"/> gives
        /// the compute rent booked at a place since a day, in cents (0 where the ledger has none).
        /// </summary>
        public static RepairReport Run(FirmsFile now, FirmsFile copy, TalentFile talent, ICollection<string> standing, Func<string, int, long> rentSince)
        {
            var rep = new RepairReport();
            for (int i = 0; now != null && i < now.Firms.Count; i++)
            {
                Firm f = now.Firms[i];
                if (standing != null && !standing.Contains(f.Place))
                {
                    continue;
                }
                Firm was = copy != null ? copy.FirmAt(f.Place) : null;
                int joined = StaffBefore(talent, f.Place, f.Founded);
                bool byCopy = was != null && was.Founded < f.Founded - Margin;
                if (joined == int.MaxValue)
                {
                    continue;   // without a staff that outlived the record there is no sign it was lost: a firm closed and founded again on purpose looks like it, but its staff is new
                }
                int made = f.Founded;
                int back = byCopy ? Math.Min(was.Founded, joined) : joined;
                f.Founded = back;
                rep.Firms++;
                rep.Steps.Add("the firm at " + f.Place + ": the record was made again on day " + made + "; its founding day is day " + back
                              + (byCopy ? " (the copy holds the firm)" : " (its professionals joined before)"));
                if (!byCopy)
                {
                    continue;   // no copy to put products back from
                }
                CopyFigures(was, f);
                long rent = 0;
                int placed = 0;
                foreach (Product bp in copy.Products)
                {
                    if (bp.Place != f.Place || bp.Stage >= 2)
                    {
                        continue;
                    }
                    Product have = Match(now, bp);
                    if (have == null)
                    {
                        Product add = Clone(bp);
                        if (now.ProductOf(add.Id) != null)
                        {
                            add.Id = now.Next();
                        }
                        now.NextId = Math.Max(now.NextId, add.Id + 1);
                        now.Products.Add(add);
                        placed++;
                        rep.Steps.Add("  product " + add.Name + " (id " + add.Id + ") put back " + (add.Stage == 1 ? "on sale since day " + add.Launched : "in build"));
                    }
                    else if (bp.Stage == 1 && have.Stage == 0)
                    {
                        have.Stage = 1;
                        have.Launched = bp.Launched;
                        have.LastRelease = Math.Max(have.LastRelease, bp.LastRelease);
                        have.Features = Math.Max(have.Features, bp.Features);
                        have.Compute = Math.Max(have.Compute, bp.Compute);
                        placed++;
                        rep.Steps.Add("  product " + have.Name + " (id " + have.Id + ") put back on sale since day " + have.Launched);
                    }
                    if (bp.K == ProductKind.Model && bp.Features > 0 && rentSince != null)
                    {
                        rent = Math.Max(rent, rentSince(f.Place, made));
                    }
                }
                rep.Products += placed;
                if (rent > 0)
                {
                    rep.RefundCents += rent;
                    rep.Steps.Add("  compute rent booked since day " + made + " paid back: " + rent + " cents");
                }
            }
            return rep;
        }

        // The product of the file that is the same as the copy's: same kind and name at the same place.
        private static Product Match(FirmsFile f, Product bp)
        {
            string key = TitleRules.Key(bp.Name);
            for (int i = 0; i < f.Products.Count; i++)
            {
                Product p = f.Products[i];
                if (p.Place == bp.Place && p.Kind == bp.Kind && p.Stage < 2 && TitleRules.Key(p.Name) == key)
                {
                    return p;
                }
            }
            return null;
        }

        private static Product Clone(Product p) => new Product
        {
            Id = p.Id, Place = p.Place, Name = p.Name, Kind = p.Kind, Segment = p.Segment, Stage = p.Stage, Features = p.Features, Progress = p.Progress, Bugs = p.Bugs, Users = p.Users,
            Free = p.Free, Tier = p.Tier, Marketing = p.Marketing, Share = p.Share, Launched = p.Launched, Hype = p.Hype, Scope = p.Scope, Revenue = p.Revenue,
            RivalFeatures = p.RivalFeatures, RivalTier = p.RivalTier, LastRelease = p.LastRelease, Carry = p.Carry, Compute = p.Compute, Train = p.Train
        };

        // What a firm's own record holds, but not its place, kind or the day-by-day takings the new record already counts from the rebuilt day.
        private static void CopyFigures(Firm from, Firm to)
        {
            to.Focus = from.Focus;
            to.Debt = from.Debt;
            to.Reputation = from.Reputation;
            to.RegisteredUntil = Math.Max(to.RegisteredUntil, from.RegisteredUntil);
            to.Breaches = Math.Max(to.Breaches, from.Breaches);
            to.Outages = Math.Max(to.Outages, from.Outages);
            to.RnD = Math.Max(to.RnD, from.RnD);
            to.ReliefDay = Math.Max(to.ReliefDay, from.ReliefDay);
        }
    }
}
