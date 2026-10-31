using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Tech
{
    public static partial class SectorRules
    {
        public const int ClientSmall = 0, ClientMid = 1, ClientLarge = 2;

        // ---------------------------------------------------------------- the city's demand for IT work

        /// <summary>
        /// The projects the city's firms and the region its IT firms serve put out a day, thousandths of a project, by client
        /// size (small, mid-sized, large): each firm's projects a year over the markets' year, times the region, times the
        /// economy. The place of the old fixed chance of an offer a day.
        /// </summary>
        public static void Demand(SectorCityFigures city, int cycle, long[] bySize)
        {
            int spend = FirmsRules.CycleSpend[Clamp(cycle, 0, 3)];
            long world = SectorCatalogue.ServicesWorld;
            bySize[ClientSmall] = (long)city.Small * SectorCatalogue.ProjectsSmall * world / 1000 * spend / 1000 / SectorCatalogue.MarketYear;
            bySize[ClientMid] = (long)city.Mid * SectorCatalogue.ProjectsMid * world / 1000 * spend / 1000 / SectorCatalogue.MarketYear;
            bySize[ClientLarge] = (long)city.Large * SectorCatalogue.ProjectsLarge * world / 1000 * spend / 1000 / SectorCatalogue.MarketYear;
        }

        /// <summary>A firm's pull in the market for IT work: its name and its sellers by the selling curve; a fresh portfolio piece draws large clients harder (see <see cref="ClientOf"/>).</summary>
        public static long Weight(Firm firm, in Crew c)
        {
            return (500L + Clamp(firm.Reputation, 0, 1000)) * (1000 + Lift(c.Selling, SectorCatalogue.SellingHalf, SectorCatalogue.SellingGain)) / 1000;
        }

        /// <summary>Every rival IT firm's pull together: the city's IT services are a crowded market.</summary>
        public static long RivalWeight() => (500L + SectorCatalogue.RivalServiceReputation) * SectorCatalogue.RivalServiceWeight / 1000;

        /// <summary>A project's client size on a roll, by the city's demand of each size; a fresh portfolio draws large clients more.</summary>
        public static int ClientOf(long[] bySize, bool portfolio, int roll)
        {
            long large = bySize[ClientLarge] * (1000 + (portfolio ? SectorCatalogue.PortfolioReach : 0)) / 1000;
            long all = bySize[ClientSmall] + bySize[ClientMid] + large;
            if (all <= 0)
            {
                return ClientSmall;
            }
            long at = all * Clamp(roll, 0, 999) / 1000;
            return at < bySize[ClientSmall] ? ClientSmall : at < bySize[ClientSmall] + bySize[ClientMid] ? ClientMid : ClientLarge;
        }

        /// <summary>A kind of work's points for a client of a size: the kind's usual range, smaller for a small client and larger for a large one.</summary>
        public static int PointsFor(WorkKind kind, int client, int roll)
        {
            int k = Clamp((int)kind, 0, (int)WorkKind.Count - 1);
            int least = FirmsCatalogue.WorkLeast[k], most = FirmsCatalogue.WorkMost[k];
            int points = least + (most - least) * Clamp(roll, 0, 999) / 999;
            int scale = client == ClientSmall ? SectorCatalogue.ClientSmallWork : client == ClientLarge ? SectorCatalogue.ClientLargeWork : 1000;
            return Math.Max(1, points * scale / 1000);
        }

        /// <summary>A kind of work out of a firm's kinds on a roll, each as likely.</summary>
        public static WorkKind KindOf(int kinds, int roll)
        {
            int count = 0;
            for (int k = 0; k < (int)WorkKind.Count; k++)
            {
                count += (kinds & (1 << k)) != 0 ? 1 : 0;
            }
            int n = Clamp(roll, 0, 999) * Math.Max(1, count) / 1000;
            for (int k = 0; k < (int)WorkKind.Count; k++)
            {
                if ((kinds & (1 << k)) != 0 && n-- == 0)
                {
                    return (WorkKind)k;
                }
            }
            return WorkKind.Website;
        }

        /// <summary>
        /// The day's offers under Living population (the place of FirmsRules.Offers): the city's projects shared among the
        /// player's firms that take work and the rival IT firms by their pull, each firm's share carried until a whole offer
        /// is earned (a known pace, never a daily draw); a client's size by the city's demand (a fresh portfolio draws larger
        /// ones), its project's points by its size; some clients the player's own businesses; the council's tender as before.
        /// Within the offers open at once and the contracts kept (raised, said in the app).
        /// </summary>
        public static void Offers(FirmsFile ff, SectorFile f, int day, int cycle, IList<Crew> crews, IList<KeyValuePair<string, string>> own, List<WorkNote> notes)
        {
            var bySize = new long[3];
            Demand(f.City, cycle, bySize);
            long projects = bySize[ClientSmall] + bySize[ClientMid] + bySize[ClientLarge];
            long all = RivalWeight();
            bool taker = false;
            for (int i = 0; i < ff.Firms.Count; i++)
            {
                if (FirmsCatalogue.TakesWork[Clamp(ff.Firms[i].Kind, 0, (int)FirmKind.Count - 1)] != 0)
                {
                    all += Weight(ff.Firms[i], i < crews.Count ? crews[i] : default(Crew));
                    taker = true;
                }
            }
            int offered = 0;
            for (int i = 0; i < ff.Contracts.Count; i++)
            {
                offered += ff.Contracts[i].Stage <= (int)WorkStage.Bid ? 1 : 0;
            }
            for (int i = 0; i < ff.Firms.Count; i++)
            {
                Firm firm = ff.Firms[i];
                int kinds = FirmsCatalogue.TakesWork[Clamp(firm.Kind, 0, (int)FirmKind.Count - 1)];
                if (kinds == 0)
                {
                    continue;
                }
                SectorFirm sf = f.FirmFor(firm.Place);
                long weight = Weight(firm, i < crews.Count ? crews[i] : default(Crew));
                sf.OfferCarry += projects * weight / Math.Max(1L, all) * 1000;   // millionths of an offer
                if (sf.OfferCarry < 1000000)
                {
                    continue;
                }
                sf.OfferCarry = Math.Min(sf.OfferCarry - 1000000, 999999L);   // one offer a day at the most; the rest of a busy day is not banked
                if (offered >= FirmsCatalogue.OffersMost || ff.Contracts.Count >= SectorCatalogue.ContractsMost)
                {
                    continue;   // the list is full: the client goes to a rival
                }
                WorkKind kind = KindOf(kinds, TechRandom.Mille(ff.Seed, day, 72, i));
                int client = ClientOf(bySize, day < sf.PortfolioUntil, TechRandom.Mille(ff.Seed, day, 70, i));
                bool mine = own != null && own.Count > 0 && TechRandom.Mille(ff.Seed, day, 73, i) < FirmsCatalogue.OwnShare;
                var w = new Contract { Id = ff.Next(), Kind = (int)kind, Stage = (int)WorkStage.Offered, End = day + FirmsCatalogue.OfferDays, Place = firm.Place };
                if (mine)
                {
                    KeyValuePair<string, string> c = own[TechRandom.Range(ff.Seed, day, 74, i, 0, own.Count - 1)];
                    w.OwnPlace = c.Key;
                    w.Client = c.Value;
                }
                else
                {
                    w.Client = FirmsCatalogue.Clients[TechRandom.Range(ff.Seed, day, 75, i, 0, FirmsCatalogue.Clients.Length - 1)];
                }
                w.Points = kind == WorkKind.Managed ? 0 : PointsFor(kind, client, TechRandom.Mille(ff.Seed, day, 76, i));
                w.Value = kind == WorkKind.Managed
                    ? FirmsCatalogue.ManagedMonth * (client == ClientSmall ? SectorCatalogue.ClientSmallWork : client == ClientLarge ? SectorCatalogue.ClientLargeWork : 1000) / 1000
                    : FirmsRules.ValueOf(kind, w.Points, false);
                ff.Contracts.Add(w);
                offered++;
                notes?.Add(new WorkNote { Contract = w.Id, Kind = FirmsRules.NoteOffered });
            }
            if (taker && day - ff.LastTender >= FirmsCatalogue.TenderEvery && ff.Contracts.Count < SectorCatalogue.ContractsMost)
            {
                ff.LastTender = day;
                WorkKind kind = TechRandom.Mille(ff.Seed, day, 77) < 500 ? WorkKind.Integration : WorkKind.Managed;
                var t = new Contract
                {
                    Id = ff.Next(), Kind = (int)kind, Client = FirmsCatalogue.Council, Council = true, Stage = (int)WorkStage.Tender, End = day + FirmsCatalogue.TenderDays,
                    Points = kind == WorkKind.Managed ? 0 : TechRandom.Range(ff.Seed, day, 78, 0, FirmsCatalogue.WorkLeast[(int)kind], FirmsCatalogue.WorkMost[(int)kind]),
                    Rivals = TechRandom.Range(ff.Seed, day, 79, 0, 2, 4)
                };
                t.Value = FirmsRules.ValueOf(kind, t.Points, true);
                ff.Contracts.Add(t);
                notes?.Add(new WorkNote { Contract = t.Id, Kind = FirmsRules.NoteTender });
            }
        }

        /// <summary>The offers a firm's share of the city's IT work brings a month, thousandths of an offer: what its page says to expect.</summary>
        public static long OffersAMonth(FirmsFile ff, SectorFile f, Firm firm, in Crew c, IList<Crew> crews, int cycle)
        {
            if (FirmsCatalogue.TakesWork[Clamp(firm.Kind, 0, (int)FirmKind.Count - 1)] == 0)
            {
                return 0;
            }
            var bySize = new long[3];
            Demand(f.City, cycle, bySize);
            long all = RivalWeight();
            for (int i = 0; i < ff.Firms.Count; i++)
            {
                if (FirmsCatalogue.TakesWork[Clamp(ff.Firms[i].Kind, 0, (int)FirmKind.Count - 1)] != 0)
                {
                    all += Weight(ff.Firms[i], i < crews.Count ? crews[i] : default(Crew));
                }
            }
            return (bySize[ClientSmall] + bySize[ClientMid] + bySize[ClientLarge]) * Weight(firm, c) / Math.Max(1L, all) * FirmsCatalogue.MonthDays;
        }

        // ---------------------------------------------------------------- the security firms' clients attacked

        /// <summary>
        /// A security firm's managed clients are attacked now and then (seeded): each attack is an incident offered to the firm
        /// that watches over the client, with a short time to answer it. Returns how many were offered.
        /// </summary>
        public static int Incidents(FirmsFile ff, int day, List<WorkNote> notes)
        {
            int n = 0, count = ff.Contracts.Count;
            for (int i = 0; i < count && ff.Contracts.Count < SectorCatalogue.ContractsMost; i++)
            {
                Contract m = ff.Contracts[i];
                Firm firm = m.Stage == (int)WorkStage.Active && m.Monthly ? ff.FirmAt(m.Place) : null;
                if (firm == null || firm.K != FirmKind.Security || TechRandom.Mille(ff.Seed, m.Id, day, 91) >= SectorCatalogue.IncidentChance)
                {
                    continue;
                }
                var w = new Contract
                {
                    Id = ff.Next(), Place = firm.Place, Kind = (int)WorkKind.Incident, Client = m.Client, OwnPlace = m.OwnPlace, Stage = (int)WorkStage.Offered,
                    End = day + 1, Points = SectorCatalogue.IncidentPoints, Value = FirmsRules.ValueOf(WorkKind.Incident, SectorCatalogue.IncidentPoints, false)
                };
                ff.Contracts.Add(w);
                notes?.Add(new WorkNote { Contract = w.Id, Kind = FirmsRules.NoteOffered });
                n++;
            }
            return n;
        }

        /// <summary>An incident taken on is due in days, not a week: an attack is answered now.</summary>
        public static void Urgent(Contract w, int day)
        {
            if (w != null && w.K == WorkKind.Incident && w.Stage == (int)WorkStage.Active)
            {
                w.Due = Math.Min(w.Due, day + SectorCatalogue.IncidentDays);
            }
        }
    }
}
