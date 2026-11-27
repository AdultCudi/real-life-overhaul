using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Tech
{
    /// <summary>
    /// The three new kinds of client of a tech firm: the city's departments (large, slow to pay, some of them public tenders rival firms bid on), individuals (small, quick,
    /// some pay late and some ask for one more change) and the city's own companies (work that fits what the company does). Game-free. The day's offers are drawn by the old
    /// rules (FirmsRules.Offers, SectorRules.Offers) and <see cref="Mix"/> then gives each offer its client, so the number of offers and the work the city puts out do not
    /// change, only who asks and on what terms. Every roll is this part's own (streams 201 to 213 of <see cref="TechRandom"/>), so no older roll moves.
    /// </summary>
    public static class TechClientRules
    {
        private const int StreamKind = 201, StreamTrade = 202, StreamBrief = 203, StreamName = 204, StreamSpread = 205, StreamLag = 206, StreamLate = 207, StreamCreep = 208, StreamWeigh = 209;

        public const int ShortSecurity = 1, ShortReliability = 2;

        // ------------------------------------------------------------------------------- who asks

        /// <summary>The share of a day's offers (thousandths) that come from departments, individuals and companies for a firm of this name; the old clients keep ClassicShare.</summary>
        public static void Shares(int reputation, WorkKind work, bool governmentOpen, out int government, out int individual, out int company)
        {
            int name = Math.Max(0, Math.Min(1000, reputation));
            individual = Math.Max(ClientCatalogue.IndividualLeast, Math.Min(ClientCatalogue.IndividualMost, ClientCatalogue.IndividualBase - name / 5));
            government = Math.Min(ClientCatalogue.GovernmentMost, ClientCatalogue.GovernmentBase + name / 8);
            if (!governmentOpen || Count(ClientKind.Government, work, ClientTrade.None, false) == 0)
            {
                government = 0;
            }
            if (work == WorkKind.Managed || Count(ClientKind.Individual, work, ClientTrade.None, false) == 0)
            {
                individual = 0;   // a monthly service is no small client's: it needs a team on call, which a baker's site never pays for
            }
            company = Trades(work, null) == 0 ? 0 : Math.Max(0, 1000 - ClientCatalogue.ClassicShare - government - individual);
        }

        /// <summary>How many briefs a kind of client has for a kind of work (of one trade where one is given; of tenders or of direct offers for a department).</summary>
        public static int Count(ClientKind kind, WorkKind work, ClientTrade trade, bool tender)
        {
            int n = 0;
            for (int i = 0; i < ClientCatalogue.Briefs.Length; i++)
            {
                n += Fits(ClientCatalogue.Briefs[i], kind, work, trade, tender) ? 1 : 0;
            }
            return n;
        }

        private static bool Fits(ClientBrief b, ClientKind kind, WorkKind work, ClientTrade trade, bool tender) =>
            b.Kind == kind && b.Work == work && (kind != ClientKind.Company || trade == ClientTrade.None || b.Trade == trade) && (kind != ClientKind.Government || b.Tender == tender)
            && (kind != ClientKind.Company || ClientFilter.Pool(b.Trade).Length > 0);

        // The trades with a brief for a kind of work and a company to write it; the nth of them when into is given a place to say which.
        private static int Trades(WorkKind work, int[] into)
        {
            int n = 0;
            for (int t = 0; t < (int)ClientTrade.Count; t++)
            {
                if (Count(ClientKind.Company, work, (ClientTrade)t, false) > 0)
                {
                    if (into != null && n < into.Length)
                    {
                        into[n] = t;
                    }
                    n++;
                }
            }
            return n;
        }

        private static ClientBrief Pick(ClientKind kind, WorkKind work, ClientTrade trade, bool tender, int roll)
        {
            int n = Count(kind, work, trade, tender);
            if (n == 0)
            {
                return null;
            }
            int at = Math.Max(0, Math.Min(999, roll)) * n / 1000;
            for (int i = 0; i < ClientCatalogue.Briefs.Length; i++)
            {
                if (Fits(ClientCatalogue.Briefs[i], kind, work, trade, tender) && at-- == 0)
                {
                    return ClientCatalogue.Briefs[i];
                }
            }
            return null;
        }

        private static readonly int[] TradeList = new int[(int)ClientTrade.Count];

        // ------------------------------------------------------------------------------- the day's offers

        /// <summary>
        /// Gives the offers the old rules drew today (contracts from index <paramref name="from"/> on) their client: a department, an individual, a company or an old client, by the
        /// firm's name and the kind of work. A business of the player's own that a brief fits is that company; one no brief fits stays as drawn. The council's tender becomes a
        /// department's public tender, or is not put out while an audit holds public work back. Returns how many offers changed kind.
        /// </summary>
        public static int Mix(FirmsFile f, int day, int from, IList<KeyValuePair<string, string>> own, IList<string> ownTypes)
        {
            int made = 0;
            for (int i = Math.Max(0, from); i < f.Contracts.Count; i++)
            {
                Contract w = f.Contracts[i];
                if (w.Deal != 0 || w.Kind == (int)WorkKind.Incident)
                {
                    continue;
                }
                if (w.Council && w.Stage == (int)WorkStage.Tender)
                {
                    if (f.GovHold > day)
                    {
                        f.Contracts.RemoveAt(i--);   // an audit of a late public job holds the next tender back
                        continue;
                    }
                    ClientBrief tender = Pick(ClientKind.Government, w.K, ClientTrade.None, true, TechRandom.Mille(f.Seed, w.Id, StreamBrief));
                    if (tender != null)
                    {
                        Apply(f, w, tender, tender.Names[TechRandom.Range(f.Seed, w.Id, StreamName, 0, 0, tender.Names.Length - 1)], ClientTrade.None);
                        made++;
                    }
                    continue;
                }
                if (w.Stage != (int)WorkStage.Offered)
                {
                    continue;
                }
                made += MixOffer(f, w, day, own, ownTypes) ? 1 : 0;
            }
            return made;
        }

        private static bool MixOffer(FirmsFile f, Contract w, int day, IList<KeyValuePair<string, string>> own, IList<string> ownTypes)
        {
            WorkKind work = w.K;
            if (!string.IsNullOrEmpty(w.OwnPlace))
            {
                ClientTrade trade = ClientTrade.None;
                for (int o = 0; o < (own != null ? own.Count : 0) && ownTypes != null && o < ownTypes.Count; o++)
                {
                    if (own[o].Key == w.OwnPlace)
                    {
                        trade = ClientCatalogue.TradeOf(ownTypes[o]);
                        break;
                    }
                }
                ClientBrief mine = trade == ClientTrade.None ? null : Pick(ClientKind.Company, work, trade, false, TechRandom.Mille(f.Seed, w.Id, StreamBrief));
                if (mine == null)
                {
                    return false;
                }
                Apply(f, w, mine, w.Client, trade);
                return true;
            }
            Firm firm = f.FirmAt(w.Place);
            Shares(firm != null ? firm.Reputation : 500, work, f.GovHold <= day, out int government, out int individual, out int company);
            int total = ClientCatalogue.ClassicShare + government + individual + company;
            int at = TechRandom.Mille(f.Seed, w.Id, StreamKind) * total / 1000;
            ClientKind kind = at < ClientCatalogue.ClassicShare ? ClientKind.Classic : at < ClientCatalogue.ClassicShare + government ? ClientKind.Government
                : at < ClientCatalogue.ClassicShare + government + individual ? ClientKind.Individual : ClientKind.Company;
            if (kind == ClientKind.Classic)
            {
                return false;
            }
            ClientTrade of = ClientTrade.None;
            if (kind == ClientKind.Company)
            {
                int trades = Trades(work, TradeList);
                of = (ClientTrade)TradeList[Math.Min(trades - 1, TechRandom.Mille(f.Seed, w.Id, StreamTrade) * trades / 1000)];
            }
            ClientBrief b = Pick(kind, work, of, false, TechRandom.Mille(f.Seed, w.Id, StreamBrief));
            if (b == null)
            {
                return false;
            }
            string name;
            if (kind == ClientKind.Company)
            {
                string[] pool = Pool(of);
                name = pool[TechRandom.Range(f.Seed, w.Id, StreamName, 0, 0, pool.Length - 1)];
            }
            else
            {
                name = b.Names[TechRandom.Range(f.Seed, w.Id, StreamName, 0, 0, b.Names.Length - 1)];
            }
            Apply(f, w, b, name, of);
            return true;
        }

        private static readonly string[][] PoolNames = new string[(int)ClientTrade.Count][];

        // The names of a trade's companies that may be clients (ClientFilter.Pool), made once.
        private static string[] Pool(ClientTrade trade)
        {
            string[] names = PoolNames[(int)trade];
            if (names == null)
            {
                RealLifeOverhaul.Markets.CompanyDef[] pool = ClientFilter.Pool(trade);
                names = new string[pool.Length];
                for (int i = 0; i < pool.Length; i++)
                {
                    names[i] = pool[i].Name;
                }
                PoolNames[(int)trade] = names;
            }
            return names;
        }

        /// <summary>Gives a contract the client, the terms and the risks of a brief: points and pay worked from what the old rules drew, payment, scope creep and what the firm must hold.</summary>
        public static void Apply(FirmsFile f, Contract w, ClientBrief b, string client, ClientTrade trade)
        {
            int k = (int)b.Kind;
            bool tender = w.Council && w.Stage == (int)WorkStage.Tender;
            w.Deal = k;
            w.Brief = b.Id;
            w.Client = client;
            w.NeedSecurity = b.NeedSecurity;
            w.NeedReliability = b.NeedReliability;
            int rate = ClientCatalogue.RateScale[k] * (b.Kind == ClientKind.Company ? ClientCatalogue.TradeRate(trade) : 1000) / 1000;
            if (tender)
            {
                rate = 1000 * ClientCatalogue.TenderRate / FirmsCatalogue.CouncilShare;   // the base value is the council's old price (a half more): a department's tender is fairer
            }
            if (w.Monthly)
            {
                w.Value = w.Value * rate / 1000;
            }
            else
            {
                long basePoints = Math.Max(1, w.Points);
                int spread = ClientCatalogue.PointsSpread[k];
                int jitter = 1000 + (TechRandom.Mille(f.Seed, w.Id, StreamSpread) - 500) * spread / 500;
                int points = (int)Math.Max(1L, basePoints * ClientCatalogue.PointsScale[k] / 1000 * jitter / 1000);
                w.Value = w.Value * points / basePoints * rate / 1000;
                w.Points = points;
            }
            if (tender)
            {
                w.Fair = w.Value;
            }
            switch (b.Kind)
            {
                case ClientKind.Government:
                    w.Lag = TechRandom.Range(f.Seed, w.Id, StreamLag, 0, ClientCatalogue.GovernmentLagLeast, ClientCatalogue.GovernmentLagMost);
                    break;
                case ClientKind.Individual:
                    w.Lag = TechRandom.Mille(f.Seed, w.Id, StreamLate) < ClientCatalogue.IndividualLateChance
                        ? TechRandom.Range(f.Seed, w.Id, StreamLag, 1, ClientCatalogue.IndividualLagLeast, ClientCatalogue.IndividualLagMost) : 0;
                    w.CreepMille = !w.Monthly && TechRandom.Mille(f.Seed, w.Id, StreamCreep) < ClientCatalogue.IndividualCreepChance ? 700 : 0;
                    break;
                case ClientKind.Company:
                    w.Lag = TechRandom.Mille(f.Seed, w.Id, StreamLate) < ClientCatalogue.CompanyTermsChance ? ClientCatalogue.CompanyTermsDays : 0;
                    w.CreepMille = !w.Monthly && TechRandom.Mille(f.Seed, w.Id, StreamCreep) < ClientCatalogue.CompanyCreepChance ? 700 : 0;
                    break;
            }
        }

        /// <summary>
        /// One offer of a kind of client to a firm, built as <see cref="Mix"/> builds it from an offer of the old rules' middle size: for the tests and the checks.
        /// Null when no brief fits the kind and the work.
        /// </summary>
        public static Contract Make(FirmsFile f, string place, ClientKind kind, WorkKind work, bool tender, int day, int roll)
        {
            ClientBrief b;
            ClientTrade trade = ClientTrade.None;
            if (kind == ClientKind.Company)
            {
                int trades = Trades(work, TradeList);
                if (trades == 0)
                {
                    return null;
                }
                trade = (ClientTrade)TradeList[Math.Min(trades - 1, Math.Max(0, Math.Min(999, roll)) * trades / 1000)];
            }
            b = Pick(kind, work, trade, tender, roll);
            if (b == null)
            {
                return null;
            }
            int basePoints = work == WorkKind.Managed ? 0 : (FirmsCatalogue.WorkLeast[(int)work] + FirmsCatalogue.WorkMost[(int)work]) / 2;
            var w = new Contract
            {
                Id = f.Next(), Kind = (int)work, Stage = tender ? (int)WorkStage.Tender : (int)WorkStage.Offered, Council = tender, Place = tender ? string.Empty : place,
                End = day + (tender ? FirmsCatalogue.TenderDays : FirmsCatalogue.OfferDays), Points = basePoints, Rivals = tender ? 3 : 0
            };
            w.Value = FirmsRules.ValueOf(work, basePoints, tender);
            string[] pool = kind == ClientKind.Company ? Pool(trade) : b.Names;
            string name = pool[TechRandom.Range(f.Seed, w.Id, StreamName, 0, 0, pool.Length - 1)];
            Apply(f, w, b, name, trade);
            return w;
        }

        // ------------------------------------------------------------------------------- terms

        /// <summary>What the firm falls short of to take the work: a bit for each of security and reliability, 0 when it holds both.</summary>
        public static int Short(Contract w, in Crew c) =>
            (w.NeedSecurity > c.Security ? ShortSecurity : 0) | (w.NeedReliability > c.Reliability ? ShortReliability : 0);

        /// <summary>The days a project is given: the kind of work's own, by the client's pace.</summary>
        public static int Days(Contract w, int baseDays) =>
            w.Deal == 0 ? baseDays : Math.Max(3, baseDays * ClientCatalogue.DueScale[Math.Max(0, Math.Min((int)ClientKind.Count - 1, w.Deal))] / 1000);

        /// <summary>The share of a project's price paid when it is signed, thousandths.</summary>
        public static int UpfrontOf(Contract w) => w.Deal == 0 ? FirmsCatalogue.UpfrontShare : ClientCatalogue.Upfront[Math.Max(0, Math.Min((int)ClientKind.Count - 1, w.Deal))];

        /// <summary>A tender's fair price, cents: the price it was put out at (a department's), or the council's old rule's.</summary>
        public static bool HasFair(Contract w) => w.Fair > 0;

        /// <summary>The thousandths of a project's price taken off for each day it is late, and the most of the price that can be taken.</summary>
        public static int LatePenaltyOf(Contract w) => ClientCatalogue.LatePenalty[Math.Max(0, Math.Min((int)ClientKind.Count - 1, w.Deal))];

        // ------------------------------------------------------------------------------- the work

        /// <summary>
        /// A day's work on a project of a client of the new kinds: one more change asked for when the work is part done, the points put in, and on its last point the price paid
        /// less a penalty for each day late, at once or after the client's days of grace. A late public job brings an audit (a note the director turns into the hold on public
        /// work). Returns what is paid today, cents.
        /// </summary>
        public static long Project(Contract w, Firm firm, int points, int day, List<WorkNote> notes)
        {
            int k = Math.Max(0, Math.Min((int)ClientKind.Count - 1, w.Deal));
            Creep(w, notes);
            w.Done = Math.Min(w.Points, w.Done + Math.Max(0, points));
            if (w.Done >= w.Points)
            {
                int late = Math.Max(0, day - w.Due);
                long penalty = Math.Min(w.Value / 2, w.Value * ClientCatalogue.LatePenalty[k] / 1000 * late);
                long rest = Math.Max(0, w.Value - penalty - w.Paid);
                w.Stage = (int)WorkStage.Done;
                w.Satisfaction = TalentCatalogue.Clamp(900 - late * 60, 100, 1000);
                firm.Reputation = TalentCatalogue.Clamp(firm.Reputation + (late == 0 ? ClientCatalogue.ReputationOnTime[k] : -Math.Min(80, late * ClientCatalogue.ReputationLate[k])), 0, 1000);
                long now = 0;
                if (w.Lag > 0 && rest > 0)
                {
                    w.Owed = rest;
                    w.PayDay = day + w.Lag;
                    notes?.Add(new WorkNote { Contract = w.Id, Kind = FirmsRules.NoteOwed, Amount = rest });
                }
                else
                {
                    w.Paid += rest;
                    now = rest;
                    notes?.Add(new WorkNote { Contract = w.Id, Kind = FirmsRules.NoteDone, Amount = rest });
                }
                if (w.Deal == (int)ClientKind.Government && late >= ClientCatalogue.ScrutinyLateDays)
                {
                    notes?.Add(new WorkNote { Contract = w.Id, Kind = FirmsRules.NoteScrutiny, Amount = penalty });
                }
                return now;
            }
            if (day > w.Due + (w.Due - w.Start))
            {
                w.Stage = (int)WorkStage.Failed;
                firm.Reputation = Math.Max(0, firm.Reputation - ClientCatalogue.ReputationFailed[k]);
                notes?.Add(new WorkNote { Contract = w.Id, Kind = FirmsRules.NoteFailed });
            }
            return 0;
        }

        // A client that carries the risk weighs it twice, at 40 and at 75 per cent of the work: if it asks, the brief grows by a fifth of its points and the date by the same share of
        // its schedule, and the price stays. The roll is the contract's own, so a reload gives the same answer.
        private static void Creep(Contract w, List<WorkNote> notes)
        {
            if (w.CreepMille <= 0 || w.Changes >= ClientCatalogue.CreepMost || w.Points <= 0)
            {
                return;
            }
            int at = w.Changes == 0 ? ClientCatalogue.CreepFirstAt : ClientCatalogue.CreepSecondAt;
            if ((long)w.Done * 1000 < (long)w.Points * at)
            {
                return;
            }
            int weighing = w.Changes++;
            int chance = weighing == 0 ? w.CreepMille : w.CreepMille / 2;
            if (TechRandom.Mille(w.Start, w.Id, weighing, StreamWeigh) >= chance)
            {
                return;
            }
            int added = Math.Max(1, w.Points * ClientCatalogue.CreepShare / 1000);
            int span = Math.Max(1, w.Due - w.Start);
            w.Due += Math.Max(1, span * added / Math.Max(1, w.Points));
            w.Points += added;
            w.Extra += added;
            notes?.Add(new WorkNote { Contract = w.Id, Kind = FirmsRules.NoteChange, Amount = added });
        }

        /// <summary>What a delivered contract's client pays today, cents: all of what it owes once its day has come, nothing before.</summary>
        public static long Due(Contract w, int day)
        {
            if (w.Owed <= 0 || day < w.PayDay)
            {
                return 0;
            }
            long paid = w.Owed;
            w.Paid += paid;
            w.Owed = 0;
            return paid;
        }

        // ------------------------------------------------------------------------------- clients that may no longer ask

        /// <summary>
        /// Withdraws the offers, tenders and bids whose client is one of the player's own businesses of a trade a tech firm may not serve (software, IT, web or graphic design),
        /// read through <paramref name="typeOf"/> (the business type at a place, or null). Contracts under way run to their end. Returns how many were withdrawn.
        /// </summary>
        public static int Withdraw(FirmsFile f, Func<string, string> typeOf, int day)
        {
            int n = 0;
            for (int i = 0; i < f.Contracts.Count; i++)
            {
                Contract w = f.Contracts[i];
                if (string.IsNullOrEmpty(w.OwnPlace) || w.Stage > (int)WorkStage.Bid)
                {
                    continue;
                }
                string type = typeOf(w.OwnPlace);
                if (type != null && ClientFilter.TypeExcluded(type))
                {
                    w.Stage = (int)WorkStage.Declined;
                    w.End = day;
                    n++;
                }
            }
            return n;
        }
    }
}
