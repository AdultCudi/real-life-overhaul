using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Tech
{
    /// <summary>The contracts part of the firms' rules: offers, the council's tenders, the work and the money.</summary>
    public static partial class FirmsRules
    {
        /// <summary>What a contract of so many points is worth, cents: the point rate (security work dearer), a council job bigger.</summary>
        public static long ValueOf(WorkKind kind, int points, bool council)
        {
            long v = kind == WorkKind.Managed ? FirmsCatalogue.ManagedMonth
                : (long)points * (kind == WorkKind.Audit || kind == WorkKind.PenTest || kind == WorkKind.Incident ? FirmsCatalogue.SecurityRate : FirmsCatalogue.PointRate);
            return council ? v * FirmsCatalogue.CouncilShare / 1000 : v;
        }

        /// <summary>The day's offers to the firms that take contracts, from the city or the player's own businesses, and a council tender now and then.</summary>
        public static void Offers(FirmsFile f, int day, IList<Crew> crews, IList<KeyValuePair<string, string>> own, List<WorkNote> notes)
        {
            int offered = 0;
            for (int i = 0; i < f.Contracts.Count; i++)
            {
                offered += f.Contracts[i].Stage <= (int)WorkStage.Bid ? 1 : 0;
            }
            bool taker = false;
            for (int i = 0; i < f.Firms.Count && offered < FirmsCatalogue.OffersMost && f.Contracts.Count < FirmsCatalogue.ContractsMost; i++)
            {
                Firm firm = f.Firms[i];
                int kinds = FirmsCatalogue.TakesWork[TalentCatalogue.Clamp(firm.Kind, 0, (int)FirmKind.Count - 1)];
                if (kinds == 0)
                {
                    continue;
                }
                taker = true;
                Crew c = i < crews.Count ? crews[i] : default(Crew);
                int chance = FirmsCatalogue.OfferChance * (500 + firm.Reputation) / 1000 * (1000 + Math.Min(2000, c.Selling * 60)) / 1000;
                if (TechRandom.Mille(f.Seed, day, 71, i) >= chance)
                {
                    continue;
                }
                WorkKind kind = PickKind(kinds, TechRandom.Mille(f.Seed, day, 72, i));
                bool mine = own != null && own.Count > 0 && TechRandom.Mille(f.Seed, day, 73, i) < FirmsCatalogue.OwnShare;
                var w = new Contract { Id = f.Next(), Kind = (int)kind, Stage = (int)WorkStage.Offered, End = day + FirmsCatalogue.OfferDays };
                if (mine)
                {
                    KeyValuePair<string, string> client = own[TechRandom.Range(f.Seed, day, 74, i, 0, own.Count - 1)];
                    w.OwnPlace = client.Key;
                    w.Client = client.Value;
                }
                else
                {
                    w.Client = FirmsCatalogue.Clients[TechRandom.Range(f.Seed, day, 75, i, 0, FirmsCatalogue.Clients.Length - 1)];
                }
                w.Points = kind == WorkKind.Managed ? 0 : TechRandom.Range(f.Seed, day, 76, i, FirmsCatalogue.WorkLeast[(int)kind], FirmsCatalogue.WorkMost[(int)kind]);
                w.Value = ValueOf(kind, w.Points, false);
                w.Place = firm.Place;   // the firm it was offered to does the work if it is taken
                f.Contracts.Add(w);
                offered++;
                notes?.Add(new WorkNote { Contract = w.Id, Kind = NoteOffered });
            }
            if (taker && day - f.LastTender >= FirmsCatalogue.TenderEvery && f.Contracts.Count < FirmsCatalogue.ContractsMost)
            {
                f.LastTender = day;
                WorkKind kind = TechRandom.Mille(f.Seed, day, 77) < 500 ? WorkKind.Integration : WorkKind.Managed;
                var t = new Contract
                {
                    Id = f.Next(), Kind = (int)kind, Client = FirmsCatalogue.Council, Council = true, Stage = (int)WorkStage.Tender,
                    End = day + FirmsCatalogue.TenderDays,
                    Points = kind == WorkKind.Managed ? 0 : TechRandom.Range(f.Seed, day, 78, 0, FirmsCatalogue.WorkLeast[(int)kind], FirmsCatalogue.WorkMost[(int)kind]),
                    Rivals = TechRandom.Range(f.Seed, day, 79, 0, 2, 4)
                };
                t.Value = ValueOf(kind, t.Points, true);
                f.Contracts.Add(t);
                notes?.Add(new WorkNote { Contract = t.Id, Kind = NoteTender });
            }
        }

        private static WorkKind PickKind(int kinds, int roll)
        {
            int count = 0;
            for (int k = 0; k < (int)WorkKind.Count; k++)
            {
                count += (kinds & (1 << k)) != 0 ? 1 : 0;
            }
            int n = roll * Math.Max(1, count) / 1000;
            for (int k = 0; k < (int)WorkKind.Count; k++)
            {
                if ((kinds & (1 << k)) != 0 && n-- == 0)
                {
                    return (WorkKind)k;
                }
            }
            return WorkKind.Website;
        }

        /// <summary>An offer taken by a firm: under way from today, a project's first share paid. Returns what is paid now, cents.</summary>
        public static long Take(Contract w, string place, int day)
        {
            w.Place = place;
            w.Stage = (int)WorkStage.Active;
            w.Start = day;
            w.LastBilled = day;
            if (w.Monthly)
            {
                w.End = day + 12 * FirmsCatalogue.MonthDays;
                return 0;
            }
            w.Due = day + FirmsCatalogue.WorkDays[TalentCatalogue.Clamp(w.Kind, 0, (int)WorkKind.Count - 1)];
            long upfront = w.Value * FirmsCatalogue.UpfrontShare / 1000;
            w.Paid = upfront;
            return upfront;
        }

        /// <summary>The fair price of a tender, cents, and a bid at a share of it.</summary>
        public static long BidAt(Contract tender, int thousandths) => tender.Value * Math.Max(500, thousandths) / 1000;

        /// <summary>Rival bidder r's bid on a tender, cents: 85% to 125% of the fair price. <see cref="Decide"/> weighs these very bids.</summary>
        public static long RivalBid(int seed, Contract t, int r, long fair) => fair * TechRandom.Range(seed, t.Id, 81, r, 850, 1250) / 1000;

        /// <summary>A council tender's fair price, cents: its value, or the council's price once the player's bid has won and become the value.</summary>
        public static long FairOf(Contract t) =>
            t.Council && t.Stage >= (int)WorkStage.Active && t.Stage <= (int)WorkStage.Failed ? ValueOf(t.K, t.Points, true) : t.Value;

        /// <summary>A tender decided on its last day: bids weighed by reputation and office, the lowest wins. True if the player's firm won.</summary>
        public static bool Decide(FirmsFile f, Contract t, Firm bidder, int day, int office = 0)
        {
            if (t.Bid <= 0 || bidder == null)
            {
                t.Stage = (int)WorkStage.Lost;
                return false;
            }
            long mine = t.Bid * (1500 - bidder.Reputation - TalentCatalogue.Clamp(office, 0, 50)) / 1000;
            for (int r = 0; r < t.Rivals; r++)
            {
                long bid = RivalBid(f.Seed, t, r, t.Value);
                if (bid * (1500 - 550) / 1000 < mine)
                {
                    t.Stage = (int)WorkStage.Lost;
                    return false;
                }
            }
            t.Value = t.Bid;
            return true;
        }

        /// <summary>A day of a contract under way: a project's work done or failed, or a managed service billed. Returns cents paid in today.</summary>
        public static long Work(Contract w, Firm firm, int points, int day, List<WorkNote> notes)
        {
            if (w.Stage != (int)WorkStage.Active)
            {
                return 0;
            }
            if (w.Monthly)
            {
                w.Satisfaction = TalentCatalogue.Clamp(w.Satisfaction + (points >= FirmsCatalogue.ManagedPoints ? 15 : -40), 0, 1000);
                long billed = 0;
                if (day - w.LastBilled >= FirmsCatalogue.MonthDays)
                {
                    w.LastBilled = day;
                    billed = w.Value;
                    w.Paid += billed;
                    notes?.Add(new WorkNote { Contract = w.Id, Kind = NoteBilled, Amount = billed });
                }
                if (w.Satisfaction < 300 || day >= w.End)
                {
                    w.Stage = w.Satisfaction < 300 ? (int)WorkStage.Failed : (int)WorkStage.Done;
                    firm.Reputation = TalentCatalogue.Clamp(firm.Reputation + (w.Satisfaction < 300 ? -60 : 30), 0, 1000);
                    notes?.Add(new WorkNote { Contract = w.Id, Kind = w.Satisfaction < 300 ? NoteFailed : NoteDone });
                }
                return billed;
            }
            w.Done = Math.Min(w.Points, w.Done + Math.Max(0, points));
            if (w.Done >= w.Points)
            {
                int late = Math.Max(0, day - w.Due);
                long penalty = Math.Min(w.Value / 2, w.Value * FirmsCatalogue.LatePenalty / 1000 * late);
                long rest = Math.Max(0, w.Value - penalty - w.Paid);
                w.Paid += rest;
                w.Stage = (int)WorkStage.Done;
                w.Satisfaction = TalentCatalogue.Clamp(900 - late * 60, 100, 1000);
                firm.Reputation = TalentCatalogue.Clamp(firm.Reputation + (late == 0 ? 25 : -late * 5), 0, 1000);
                notes?.Add(new WorkNote { Contract = w.Id, Kind = NoteDone, Amount = rest });
                return rest;
            }
            if (day > w.Due + (w.Due - w.Start))
            {
                w.Stage = (int)WorkStage.Failed;
                firm.Reputation = Math.Max(0, firm.Reputation - 80);
                notes?.Add(new WorkNote { Contract = w.Id, Kind = NoteFailed });
            }
            return 0;
        }

        /// <summary>The points a contract asks for today: a project what is left over the days to its date and a fifth more, a managed service its share.</summary>
        public static int Need(Contract w, int day)
        {
            if (w.Stage != (int)WorkStage.Active)
            {
                return 0;
            }
            if (w.Monthly)
            {
                return FirmsCatalogue.ManagedPoints;
            }
            int left = Math.Max(0, w.Points - w.Done);
            int days = Math.Max(1, w.Due - day);
            return Math.Min(left, (left * 12 / 10 + days - 1) / days);
        }

        /// <summary>Offers and tenders past their last day lapse; finished and failed contracts are dropped a month after.</summary>
        public static void Lapse(FirmsFile f, int day, List<WorkNote> notes)
        {
            for (int i = f.Contracts.Count - 1; i >= 0; i--)
            {
                Contract w = f.Contracts[i];
                if ((w.Stage == (int)WorkStage.Offered || w.Stage == (int)WorkStage.Tender) && day > w.End)
                {
                    w.Stage = (int)WorkStage.Declined;
                    w.End = day;
                    notes?.Add(new WorkNote { Contract = w.Id, Kind = NoteLapsed });
                }
                else if (w.Stage >= (int)WorkStage.Done && day > w.End + FirmsCatalogue.MonthDays && day > w.Due + FirmsCatalogue.MonthDays)
                {
                    f.Contracts.RemoveAt(i);
                }
            }
        }
    }
}
