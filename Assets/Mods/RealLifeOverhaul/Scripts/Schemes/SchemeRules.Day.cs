using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Schemes
{
    /// <summary>What a day of the schemes did, for the director to carry out in the game: money, notices, the court, the paper.</summary>
    public enum SchemeEventKind
    {
        Washed = 0,          // Text the place, Amount out of the cash in hand, Net into the wallet after the cut and the tax
        BankReport = 1,      // Place: a day's cash sales the bank reported
        Pattern = 2,         // Place: the window's deposits reported as a pattern
        Opened = 3,          // Inquiry: opened, and nobody is told
        Letter = 4,          // Inquiry
        Visit = 5,           // Inquiry
        Dropped = 6,         // Inquiry: no case to answer
        Caution = 7,         // Inquiry: all paid back, and little enough; Net what is taken on top of it
        Findings = 8,        // Inquiry: Amount found, FromCash of it seized in hand, Net the surcharge, Text the charge, B 1 for a felony
        Talked = 9,         // Text: who
        RivalActed = 10,     // Tycoon, A the kind, Amount brought
        RivalLetter = 11,    // Tycoon, A the kind
        RivalFound = 12,     // Tycoon, A the kind, Amount taken back with its surcharge
        RivalDropped = 13,   // Tycoon
        RivalTried = 14,     // Tycoon, A the kind, B 1 convicted
        FundOffered = 15,    // Tycoon, Amount
        FundLapsed = 16,     // Tycoon
        FundPaid = 17,       // Tycoon, Amount placed, Net what comes back
        FundLost = 18,       // Tycoon, Amount
        FundRecovered = 19   // Tycoon, Amount that comes back
    }

    /// <summary>One thing a day did. A struct: the list is reused and nothing is allocated for an event.</summary>
    public struct SchemeEvent
    {
        public SchemeEventKind Kind;
        public int Inquiry, Tycoon, A, B;
        public long Amount, Net, FromCash;
        public string Text;
    }

    /// <summary>A business as the day needs it: where it is, what it takes on an average day, and whether cash crosses its counter.</summary>
    public struct SchemeTill
    {
        public string Place;
        public long Average;       // an average day's takings, whole money
        public bool Cash;          // customers pay at a till: a business that could take cash sales
    }

    /// <summary>What the game is today, as the rules need it: filled by the director once a game day, never per frame.</summary>
    public sealed class SchemeWorld
    {
        public bool On;            // new schemes may start (the option is on); off, what is open still runs its course
        public bool Session;       // a Going Public session: the rivals rest and no fund is offered
        public int Level = 1000;   // the city's price level, tenths of a point
        public int TaxPercent;     // the game's tax rate on a business's sales, per cent: taken off washed cash as off any sale
        public long Worth;         // the player's net worth, for the size of a fund's offer
        public readonly List<SchemeTill> Tills = new List<SchemeTill>(16);
        public readonly List<int> Unpaid = new List<int>(8);          // invented claims the insurer has not paid yet, by id
        public readonly bool[] RivalActive = new bool[SchemeState.Rivals];
        public readonly long[] RivalWorth = new long[SchemeState.Rivals];
        public readonly List<string> Council = new List<string>(6);   // the councillors' names: whom a rival's money is offered to

        public void Clear()
        {
            On = Session = false;
            Level = 1000;
            TaxPercent = 0;
            Worth = 0;
            Tills.Clear();
            Unpaid.Clear();
            Council.Clear();
            Array.Clear(RivalActive, 0, RivalActive.Length);
            Array.Clear(RivalWorth, 0, RivalWorth.Length);
        }

        public bool Till(string place, out SchemeTill till)
        {
            for (int i = 0; i < Tills.Count; i++)
            {
                if (Tills[i].Place == place)
                {
                    till = Tills[i];
                    return true;
                }
            }
            till = default;
            return false;
        }
    }

    // What an event moves in the player's wallet and in a rival's worth: one rule for the director that carries it out and the simulations that count it.
    public static partial class SchemeRules
    {
        /// <summary>
        /// What an event moves in the player's wallet, signed: washed cash in (never into a business's own figures), a caution's half surcharge
        /// out, a fund's return and a sham's recovered money in. 0 for the rest; a finding's money is <see cref="FindingsDue"/>.
        /// </summary>
        public static long WalletOf(SchemeEvent e)
        {
            switch (e.Kind)
            {
                case SchemeEventKind.Washed:
                case SchemeEventKind.FundPaid:
                    return Math.Max(0L, e.Net);
                case SchemeEventKind.Caution:
                    return -Math.Max(0L, e.Net);
                case SchemeEventKind.FundRecovered:
                    return Math.Max(0L, e.Amount);
                default:
                    return 0L;
            }
        }

        /// <summary>What an event moves in a rival's worth, signed: a scheme brings, being found takes back with its quarter, a fund pays its return out of it.</summary>
        public static long RivalMove(SchemeEvent e)
        {
            switch (e.Kind)
            {
                case SchemeEventKind.RivalActed:
                    return Math.Max(0L, e.Amount);
                case SchemeEventKind.RivalFound:
                    return -Math.Max(0L, e.Amount);
                case SchemeEventKind.FundPaid:
                    return -Math.Max(0L, e.Net);
                default:
                    return 0L;
            }
        }

        /// <summary>The charge a rival convicted of a kind answers: the court's own ids, by kind.</summary>
        public static string RivalCharge(SchemeKind kind)
        {
            return kind == SchemeKind.Graft ? JusticeLaw.Bribery : kind == SchemeKind.Expense ? JusticeLaw.TaxEvasion : JusticeLaw.Fraud;
        }
    }

    // The day: the trails fade, the tills wash, people talk, inquiries open, gather evidence and end, and the rivals do the same.
    public static partial class SchemeRules
    {
        public const int CatchUpMost = 20000;   // days a skipped stretch is played back at most: more than any game has days, so in effect every one

        /// <summary>
        /// Plays every day since the last one played, each exactly as it would have been, so a stretch nobody played ends as the same days
        /// played one at a time would have ended it: an inquiry that would have charged is charged, never dropped for the gap. A day costs a
        /// few loops over the state, so even a long gap is milliseconds. The first call on a save only sets the clock. The director and the
        /// checks both call this and nothing else to move the schemes on.
        /// </summary>
        public static void Day(SchemeState s, int day, SchemeWorld w, List<SchemeEvent> events)
        {
            if (s.Day < 0)
            {
                s.StartDay = s.StartDay > 0 ? s.StartDay : day;
                s.Day = day;
                return;
            }
            for (int d = Math.Max(s.Day + 1, day - CatchUpMost + 1); d <= day; d++)
            {
                One(s, d, w, events);
            }
            s.Day = Math.Max(s.Day, day);
        }

        private static void One(SchemeState s, int day, SchemeWorld w, List<SchemeEvent> events)
        {
            for (int k = 0; k < s.Heat.Length; k++)
            {
                s.Heat[k] = Math.Max(0, s.Heat[k] - SchemeCatalogue.HeatDecay);
            }
            Tills(s, day, w, events);
            People(s, day, events);
            for (int k = 0; k < SchemeCatalogue.Kinds; k++)
            {
                var kind = (SchemeKind)k;
                if (s.OpenOf(kind) == null && Count(s, kind, day) > 0 && s.Heat[k] >= Line(s.Seed, kind, day, s.Inquiries[k]))
                {
                    Open(s, kind, day, events);
                }
            }
            for (int i = 0; i < s.Cases.Count; i++)
            {
                if (s.Cases[i].Open && s.Cases[i].Stage != InquiryStage.Charged)
                {
                    Step(s, s.Cases[i], day, w, events);
                }
            }
            RivalsDay(s, day, w, events);
            Trim(s, day);
        }

        // The dressed books and the washing tills, a business at a time.
        private static void Tills(SchemeState s, int day, SchemeWorld w, List<SchemeEvent> events)
        {
            for (int i = 0; i < s.Shops.Count; i++)
            {
                SchemeShop shop = s.Shops[i];
                if (!w.Till(shop.Place, out SchemeTill till))
                {
                    continue;   // sold, closed or not read today: nothing is washed and nothing is shown there
                }
                if (shop.Lift > 0 && w.On)
                {
                    AddHeat(s, SchemeKind.Books, shop.Lift / SchemeCatalogue.LiftHeatDivisor);
                }
                if (shop.Wash <= 0 || !w.On || s.Watched || !till.Cash)
                {
                    continue;
                }
                long amount = WashRoom(till.Average, shop.Wash, s.Dirty);
                if (amount <= 0)
                {
                    continue;
                }
                long net = WashNet(amount, w.TaxPercent);
                s.Dirty -= amount;
                if (day - shop.WindowFrom >= SchemeCatalogue.PatternDays)
                {
                    shop.WindowFrom = day;
                    shop.Window = 0;
                    shop.Flagged = false;
                }
                shop.Window += amount;
                Washed(s, shop, day, net);
                AddHeat(s, SchemeKind.Wash, shop.Wash / SchemeCatalogue.WashHeatDivisor);
                events.Add(new SchemeEvent { Kind = SchemeEventKind.Washed, Text = shop.Place, Amount = amount, Net = net });
                if (amount > SchemeCatalogue.AtPrices(SchemeCatalogue.ReportFrom, w.Level))
                {
                    AddHeat(s, SchemeKind.Wash, SchemeCatalogue.HeatReport);
                    events.Add(new SchemeEvent { Kind = SchemeEventKind.BankReport, Text = shop.Place, Amount = amount });
                }
                if (!shop.Flagged && shop.Window >= SchemeCatalogue.AtPrices(SchemeCatalogue.PatternFrom, w.Level))
                {
                    shop.Flagged = true;
                    AddHeat(s, SchemeKind.Wash, SchemeCatalogue.HeatPattern);
                    events.Add(new SchemeEvent { Kind = SchemeEventKind.Pattern, Text = shop.Place, Amount = shop.Window });
                }
            }
        }

        // A window's washing at a business is one act on the trail, growing by the day: the pattern, never ten innocent days.
        private static void Washed(SchemeState s, SchemeShop shop, int day, long net)
        {
            for (int i = s.Acts.Count - 1; i >= 0; i--)
            {
                SchemeAct a = s.Acts[i];
                if (a.Kind == SchemeKind.Wash && a.Inquiry == 0 && a.Place == shop.Place && a.Day >= shop.WindowFrom)
                {
                    a.Gain += net;
                    a.Size = Math.Max(a.Size, shop.Wash * 1000 / SchemeCatalogue.WashSteps[SchemeCatalogue.WashSteps.Length - 1]);
                    return;
                }
            }
            Keep(s, new SchemeAct
            {
                Kind = SchemeKind.Wash, Day = day, Gain = net, Place = shop.Place,
                Size = shop.Wash * 1000 / SchemeCatalogue.WashSteps[SchemeCatalogue.WashSteps.Length - 1]
            });
        }

        // Those who were paid: each may talk, more so under an inquiry; what they knew goes stale in time.
        private static void People(SchemeState s, int day, List<SchemeEvent> events)
        {
            SchemeInquiry graft = s.OpenOf(SchemeKind.Graft);
            for (int i = s.Paid.Count - 1; i >= 0; i--)
            {
                SchemePaid p = s.Paid[i];
                if (day - p.Day > SchemeCatalogue.HoldDays)
                {
                    s.Paid.RemoveAt(i);
                    continue;
                }
                if (p.Talked || !Talks(s.Seed, p.Who, day, graft != null))
                {
                    continue;
                }
                p.Talked = true;
                AddHeat(s, SchemeKind.Graft, SchemeCatalogue.HeatTalk);
                if (graft != null)
                {
                    graft.Pressed += SchemeCatalogue.EvidenceTalk;
                }
                events.Add(new SchemeEvent { Kind = SchemeEventKind.Talked, Text = p.Who, Amount = p.Amount, A = (int)p.What });
            }
        }

        private static SchemeInquiry Open(SchemeState s, SchemeKind kind, int day, List<SchemeEvent> events)
        {
            var q = new SchemeInquiry { Id = s.NextInquiry++, Kind = kind, Opened = day, Stage = InquiryStage.Quiet };
            s.Inquiries[(int)kind]++;
            s.Cases.Add(q);
            events.Add(new SchemeEvent { Kind = SchemeEventKind.Opened, Inquiry = q.Id, A = (int)kind });
            return q;
        }

        /// <summary>What a file holds against the player today, thousandths: the acts on it, their size, who knows, their paper, less what was paid back.</summary>
        public static int Holds(SchemeState s, SchemeInquiry q, int day)
        {
            if (q.Coop)
            {
                return SchemeCatalogue.CoopEvidence;
            }
            int holds = SchemeCatalogue.EvidenceBase + q.Pressed - (q.Lost ? SchemeCatalogue.LostFile : 0);
            for (int i = 0; i < s.Acts.Count; i++)
            {
                SchemeAct a = s.Acts[i];
                if (!OnFile(a, q.Kind, day))
                {
                    continue;
                }
                holds += SchemeCatalogue.EvidenceAct + a.Size * SchemeCatalogue.EvidenceSize / 1000 + a.Knows * SchemeCatalogue.EvidenceKnows
                         + (a.Paper ? SchemeCatalogue.EvidencePaper : 0) - (a.Gain > 0 && a.Owed == 0 ? SchemeCatalogue.EvidenceRepaid : 0);
            }
            return SchemeCatalogue.Clamp(holds, 0, SchemeCatalogue.EvidenceMost);
        }

        // One day of an inquiry: the evidence gathers, the signs come in their order, and it ends in findings, a caution or nothing.
        private static void Step(SchemeState s, SchemeInquiry q, int day, SchemeWorld w, List<SchemeEvent> events)
        {
            int days = day - q.Opened, holds = Holds(s, q, day);
            q.Evidence = q.Coop || q.Evidence > holds ? holds : Math.Min(holds, q.Evidence + SchemeCatalogue.EvidencePace);
            if (q.Stage == InquiryStage.Quiet && (days >= SchemeCatalogue.LetterAfter || q.Coop))
            {
                q.Stage = InquiryStage.Letter;
                events.Add(new SchemeEvent { Kind = SchemeEventKind.Letter, Inquiry = q.Id, A = (int)q.Kind });
            }
            else if (q.Stage == InquiryStage.Letter && days >= SchemeCatalogue.VisitAfter)
            {
                q.Stage = InquiryStage.Visit;
                s.Watched |= q.Kind == SchemeKind.Club || q.Kind == SchemeKind.Wash;
                events.Add(new SchemeEvent { Kind = SchemeEventKind.Visit, Inquiry = q.Id, A = (int)q.Kind });
            }
            if (!q.Known)
            {
                return;   // nothing ends before the letter: the player always sees it coming
            }
            if (Count(s, q.Kind, day) == 0)
            {
                Close(s, q, InquiryEnd.Dropped, day);   // everything it was about is older than a file reaches: no case to answer
                events.Add(new SchemeEvent { Kind = SchemeEventKind.Dropped, Inquiry = q.Id, A = (int)q.Kind });
                return;
            }
            bool seen = q.Coop || days >= SchemeCatalogue.LetterAfter + 2;
            if (seen && !q.Coop && q.Repaid > 0 && Owed(s, q.Kind, day) == 0 && q.Kind != SchemeKind.Wash && q.Kind != SchemeKind.Graft
                && !Felony(s, q.Kind, day, w.Level) && (s.Cautioned & (1 << (int)q.Kind)) == 0)
            {
                Caution(s, q, day, events);
                return;
            }
            if (seen && q.Evidence >= SchemeCatalogue.ChargeFrom && (q.Coop || q.Evidence >= SchemeCatalogue.ChargeAtOnce || days >= SchemeCatalogue.ChargeAfter))
            {
                Find(s, q, day, w, events);
                return;
            }
            if (days >= SchemeCatalogue.GiveUpAfter && q.Evidence < SchemeCatalogue.ChargeFrom)
            {
                Close(s, q, InquiryEnd.Dropped, day);
                events.Add(new SchemeEvent { Kind = SchemeEventKind.Dropped, Inquiry = q.Id, A = (int)q.Kind });
            }
        }

        // A caution: everything on the file was paid back in time, it is little, and it is the first of its kind. The acts are
        // dealt with (never found again), half the surcharge is taken on what was paid back, and no court hears of it. A
        // warning is given once: the next file of the kind goes to court, paid back or not.
        private static void Caution(SchemeState s, SchemeInquiry q, int day, List<SchemeEvent> events)
        {
            s.Cautioned |= 1 << (int)q.Kind;
            for (int i = 0; i < s.Acts.Count; i++)
            {
                if (OnFile(s.Acts[i], q.Kind, day))
                {
                    s.Acts[i].Inquiry = q.Id;
                }
            }
            q.Surcharge = SchemeCatalogue.Share(q.Repaid, SchemeCatalogue.SurchargeRepaidPermille);
            Close(s, q, InquiryEnd.Caution, day);
            events.Add(new SchemeEvent { Kind = SchemeEventKind.Caution, Inquiry = q.Id, A = (int)q.Kind, Net = q.Surcharge });
        }

        // The findings: every act on file is found once, what it still owes is taken back (the cash in hand first) with the
        // surcharge on top, and the file goes to the court. An invented claim the insurer has not paid yet is refused instead.
        private static void Find(SchemeState s, SchemeInquiry q, int day, SchemeWorld w, List<SchemeEvent> events)
        {
            string charge = ChargeOf(s, q.Kind, day, w.Level, q.Coop);   // by what the file holds, before its acts are marked as found
            bool felony = Felony(s, q.Kind, day, w.Level);
            long found = 0, washed = 0, paid = 0;
            for (int i = 0; i < s.Acts.Count; i++)
            {
                SchemeAct a = s.Acts[i];
                if (!OnFile(a, q.Kind, day))
                {
                    continue;
                }
                a.Inquiry = q.Id;
                if (q.Kind == SchemeKind.Claim && w.Unpaid.Contains(a.Ref))
                {
                    a.Gain = a.Back;   // never paid: the insurer refuses it, and there is nothing to take back
                    continue;
                }
                if (q.Kind == SchemeKind.Wash)
                {
                    washed += a.Owed;
                    a.Back = a.Gain;
                    continue;
                }
                paid += a.Paid;
                if (q.Kind != SchemeKind.Club)
                {
                    found += a.Owed;   // a club's is what the households are still out (the ledger), taken below
                }
                a.Back = a.Gain;
            }
            long surchargeOn;
            if (q.Kind == SchemeKind.Club)
            {
                found = Recover(s, ClubOwed(s));
                surchargeOn = found;
            }
            else if (q.Kind == SchemeKind.Wash)
            {
                found = Recover(s, washed);   // the washed money was the households': what they are still out goes back, once
                surchargeOn = washed;
            }
            else
            {
                surchargeOn = q.Kind == SchemeKind.Graft ? paid : found;
            }
            long fromCash = 0;
            if (q.Kind == SchemeKind.Club || q.Kind == SchemeKind.Wash)
            {
                fromCash = Math.Min(s.Dirty, found);
                s.Dirty -= fromCash;
            }
            q.Found = found;
            q.Surcharge = SchemeCatalogue.Share(surchargeOn, SchemeCatalogue.SurchargePermille) + SchemeCatalogue.Share(q.Repaid, SchemeCatalogue.SurchargeRepaidPermille);
            q.Stage = InquiryStage.Charged;
            q.End = InquiryEnd.Charged;
            events.Add(new SchemeEvent
            {
                Kind = SchemeEventKind.Findings, Inquiry = q.Id, A = (int)q.Kind, B = felony ? 1 : 0, Amount = found, FromCash = fromCash, Net = q.Surcharge, Text = charge
            });
        }

        /// <summary>
        /// An inquiry is over: by the court's verdict on what it charged, or with no court to charge in. A conviction or a
        /// settlement leaves the trail known; a file that came to nothing cools it. The cash in hand is free again once
        /// nobody is looking at it.
        /// </summary>
        public static void Close(SchemeState s, SchemeInquiry q, InquiryEnd end, int day)
        {
            if (q == null || !q.Open)
            {
                return;
            }
            q.Stage = InquiryStage.Closed;
            q.End = end;
            q.Closed = day;
            int k = (int)q.Kind;
            if (end == InquiryEnd.Cleared || end == InquiryEnd.Caution)
            {
                s.Heat[k] = s.Heat[k] * SchemeCatalogue.ClearedPermille / 1000;
            }
            else if (end == InquiryEnd.Dropped)
            {
                s.Heat[k] = s.Heat[k] * SchemeCatalogue.GaveUpPermille / 1000;
            }
            else
            {
                s.Heat[k] = Math.Min(s.Heat[k], SchemeCatalogue.OpenLeast);   // found out: known, and watched from here on
            }
            SchemeInquiry club = s.OpenOf(SchemeKind.Club), wash = s.OpenOf(SchemeKind.Wash);
            s.Watched = (club != null && club.Stage >= InquiryStage.Visit) || (wash != null && wash.Stage >= InquiryStage.Visit);
        }

        /// <summary>
        /// How an inquiry that was charged ends today: 0 while its case is still to be heard, else the <see cref="InquiryEnd"/> as a number.
        /// With no court running (justice switched off, the courts not loaded) or no case opened there is nothing left to wait for, and the
        /// inquiry ends as settled, so the cash in hand it watched is free again. Otherwise the court's own answer (<paramref name="caseEnd"/>).
        /// </summary>
        public static int Verdict(SchemeInquiry q, bool courtRuns, int caseEnd)
        {
            if (q == null || q.Stage != InquiryStage.Charged)
            {
                return 0;
            }
            if (!courtRuns || q.Case <= 0)
            {
                return (int)InquiryEnd.Settled;
            }
            return caseEnd == 0 ? 0 : SchemeCatalogue.Clamp(caseEnd, (int)InquiryEnd.Dropped, (int)InquiryEnd.Settled);
        }

        /// <summary>The player tells the investigators everything: the file is complete at once, and the charge is the lesser one.</summary>
        public static bool Cooperate(SchemeInquiry q)
        {
            if (q == null || !q.Open || !q.Known || q.Stage == InquiryStage.Charged || q.Coop)
            {
                return false;
            }
            q.Coop = true;
            return true;
        }

        /// <summary>The investigator of an open inquiry took the money: part of the file is lost.</summary>
        public static void LoseFile(SchemeInquiry q)
        {
            if (q != null && q.Open)
            {
                q.Lost = true;
            }
        }

        /// <summary>
        /// For the tests: an inquiry into a kind opens now with its letter, whatever the heat, if none is open and something
        /// is on file. Null when there is nothing to open.
        /// </summary>
        public static SchemeInquiry OpenNow(SchemeState s, SchemeKind kind, int day, List<SchemeEvent> events)
        {
            if (s.OpenOf(kind) != null || Count(s, kind, day) == 0)
            {
                return null;
            }
            SchemeInquiry q = Open(s, kind, day, events);
            q.Stage = InquiryStage.Letter;
            events.Add(new SchemeEvent { Kind = SchemeEventKind.Letter, Inquiry = q.Id, A = (int)kind });
            return q;
        }

        /// <summary>For the tests: every open inquiry is a day older and plays that day now. How many there were.</summary>
        public static int Hurry(SchemeState s, int day, SchemeWorld w, List<SchemeEvent> events)
        {
            int open = 0;
            for (int i = 0; i < s.Cases.Count; i++)
            {
                SchemeInquiry q = s.Cases[i];
                if (q.Open && q.Stage != InquiryStage.Charged)
                {
                    q.Opened--;
                    Step(s, q, day, w, events);
                    open++;
                }
            }
            return open;
        }

        // What is too old to be found goes off the trails, and the oldest closed inquiries off the list.
        private static void Trim(SchemeState s, int day)
        {
            for (int i = s.Acts.Count - 1; i >= 0; i--)
            {
                if (day - s.Acts[i].Day > SchemeCatalogue.TrailDays)
                {
                    s.Acts.RemoveAt(i);
                }
            }
            int closed = 0;
            for (int i = s.Cases.Count - 1; i >= 0; i--)
            {
                if (!s.Cases[i].Open && ++closed > SchemeCatalogue.InquiriesKept)
                {
                    s.Cases.RemoveAt(i);
                }
            }
        }
    }
}
