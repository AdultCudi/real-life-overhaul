using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Schemes
{
    // The rivals: the same schemes by seeded rules, the same heat, the same line, the same evidence and the same court's sum.
    public static partial class SchemeRules
    {
        /// <summary>What the paper prints of a rival: 1 looked into, 2 found out and charged, 3 convicted, 4 cleared.</summary>
        public const int RivalNewsLetter = 1, RivalNewsFound = 2, RivalNewsConvicted = 3, RivalNewsCleared = 4;

        private static readonly SchemeKind[] RivalKinds = { SchemeKind.Claim, SchemeKind.Expense, SchemeKind.Books, SchemeKind.Club, SchemeKind.Graft };

        /// <summary>How bent a rival is, thousandths a look: 0 for one who never schemes. A trait of the rival, fixed by the save's seed.</summary>
        public static int Bent(int seed, int tycoon)
        {
            if (Roll(seed, DrawBent, tycoon, 0) < SchemeCatalogue.RivalCleanPermille)
            {
                return 0;
            }
            return SchemeCatalogue.RivalBentLeast + Roll(seed, DrawBent, tycoon, 1) * SchemeCatalogue.RivalBentSpread / 1000;
        }

        /// <summary>What a scheme of a size brings a rival: a share of their worth, bounded.</summary>
        public static long RivalGain(long worth, int size, int level)
        {
            long gain = Math.Max(0L, worth) * (size >= SchemeCatalogue.Large ? SchemeCatalogue.RivalLargeGain : SchemeCatalogue.RivalSmallGain) / 10000L;
            return Round(Math.Min(gain, SchemeCatalogue.AtPrices(SchemeCatalogue.RivalGainMost, level)));
        }

        /// <summary>The sum a rival's fund asks of the player: a share of their worth between its least and most, in round hundreds.</summary>
        public static long FundAmount(long worth, int level)
        {
            long amount = Math.Max(SchemeCatalogue.AtPrices(SchemeCatalogue.FundLeast, level),
                Math.Min(SchemeCatalogue.AtPrices(SchemeCatalogue.FundMost, level), SchemeCatalogue.Share(worth, SchemeCatalogue.FundSharePermille)));
            return amount / 100 * 100;
        }

        /// <summary>What having a fund looked at costs.</summary>
        public static long FundFee(long amount, int level)
        {
            return Round(Math.Max(SchemeCatalogue.AtPrices(SchemeCatalogue.FundFeeLeast, level), SchemeCatalogue.Share(amount, SchemeCatalogue.FundFeePermille)));
        }

        /// <summary>What an honest fund pays back.</summary>
        public static long FundBack(long amount)
        {
            return amount + SchemeCatalogue.Share(amount, SchemeCatalogue.FundReturn);
        }

        private static void RivalsDay(SchemeState s, int day, SchemeWorld w, List<SchemeEvent> events)
        {
            for (int i = 0; i < s.Rival.Length; i++)
            {
                SchemeRival r = s.Rival[i];
                r.Heat = Math.Max(0, r.Heat - SchemeCatalogue.HeatDecay);
                if (w.Session)
                {
                    continue;   // the rivals rest in a session: nothing of theirs moves, and nothing is forgotten
                }
                if (w.RivalActive[i] && w.On && r.Stage == InquiryStage.Closed && r.TrialDay == 0 && (day + i * 10) % SchemeCatalogue.RivalEveryDays == 0)
                {
                    RivalLook(s, r, i, day, w, events);
                }
                RivalInquiry(s, r, i, day, w, events);
            }
            FundDay(s, day, w, events);
        }

        // A rival's look: whether they scheme today, which kind, how large; its heat by the player's own rule.
        private static void RivalLook(SchemeState s, SchemeRival r, int tycoon, int day, SchemeWorld w, List<SchemeEvent> events)
        {
            int bent = Bent(s.Seed, tycoon);
            if (bent <= 0 || Roll(s.Seed, DrawRival, tycoon, day) >= bent)
            {
                return;
            }
            SchemeKind kind = RivalKinds[Roll(s.Seed, DrawRivalKind, tycoon, day) % RivalKinds.Length];
            int size = Roll(s.Seed, DrawRivalSize, tycoon, day) < SchemeCatalogue.RivalLargePermille ? SchemeCatalogue.Large : SchemeCatalogue.Small;
            long gain = kind == SchemeKind.Graft ? 0L : RivalGain(w.RivalWorth[tycoon], size, w.Level);
            RivalActs(s, r, tycoon, kind, size, gain, day);
            if (kind == SchemeKind.Graft && w.Council.Count > 0)
            {
                // The councillor a rival's money goes to is one of the city's, with the temper they have for anybody who asks.
                string seat = Councillor(w.Council[Seat(s.Seed, tycoon * 1000 + day, w.Council.Count)]);
                if (TemperOf(s.Seed, Office.Councillor, seat) == Temper.Reports)
                {
                    r.Heat = Math.Min(SchemeCatalogue.HeatMost, r.Heat + SchemeCatalogue.HeatReported);
                }
            }
            events.Add(new SchemeEvent { Kind = SchemeEventKind.RivalActed, Tycoon = tycoon, A = (int)kind, Amount = gain });
        }

        // An act on a rival's trail: counted, its gain kept to be found, its heat the player's own formula.
        private static void RivalActs(SchemeState s, SchemeRival r, int tycoon, SchemeKind kind, int size, long gain, int day)
        {
            bool lately = day - r.LastAct <= SchemeCatalogue.RepeatDays;   // their schemes on file count as one pattern: recent if the newest is, older if not
            int heat = HeatOf(kind, size, lately ? r.Acts : 0, lately ? 0 : r.Acts, 1, true)
                       + (Spotted(s.Seed ^ (tycoon + 1) * 7919, kind, r.Acts + r.Opened * 31) ? SchemeCatalogue.HeatSpotted : 0);
            r.Heat = Math.Min(SchemeCatalogue.HeatMost, r.Heat + heat);
            r.Acts++;
            r.LastAct = day;
            r.Gained += Math.Max(0L, gain);
            r.Size = Math.Max(r.Size, size);
            r.NewsKind = kind;
        }

        // A rival's inquiry: opened at the same line, its evidence at the same pace, the same days to the letter, the findings and the court.
        private static void RivalInquiry(SchemeState s, SchemeRival r, int tycoon, int day, SchemeWorld w, List<SchemeEvent> events)
        {
            int seed = s.Seed ^ (tycoon + 1) * 7919;
            if (r.TrialDay > 0)
            {
                if (day < r.TrialDay)
                {
                    return;
                }
                bool convicted = Tried(seed, r.Opened, r.Evidence, r.Convictions, SchemeCatalogue.RivalCounsel);
                r.TrialDay = 0;
                r.Stage = InquiryStage.Closed;
                r.Convictions += convicted ? 1 : 0;
                r.Heat = convicted ? Math.Min(r.Heat, SchemeCatalogue.OpenLeast) : r.Heat * SchemeCatalogue.ClearedPermille / 1000;
                RivalNews(r, convicted ? RivalNewsConvicted : RivalNewsCleared, day, r.Found);
                events.Add(new SchemeEvent { Kind = SchemeEventKind.RivalTried, Tycoon = tycoon, A = (int)r.NewsKind, B = convicted ? 1 : 0, Amount = r.Found });
                return;
            }
            if (r.Stage == InquiryStage.Closed)
            {
                if (r.Acts > 0 && r.Heat >= Line(seed, r.NewsKind, day, r.Opened))
                {
                    r.Stage = InquiryStage.Quiet;
                    r.Since = day;
                    r.Evidence = 0;
                    r.Opened++;
                }
                return;
            }
            int days = day - r.Since;
            int holds = SchemeCatalogue.Clamp(SchemeCatalogue.EvidenceBase + r.Acts * (SchemeCatalogue.EvidenceAct + SchemeCatalogue.EvidenceKnows + SchemeCatalogue.EvidencePaper)
                                              + r.Size * SchemeCatalogue.EvidenceSize / 1000, 0, SchemeCatalogue.EvidenceMost);
            r.Evidence = Math.Min(holds, r.Evidence + SchemeCatalogue.EvidencePace);
            if (r.Stage == InquiryStage.Quiet && days >= SchemeCatalogue.LetterAfter)
            {
                r.Stage = InquiryStage.Letter;
                RivalNews(r, RivalNewsLetter, day, 0);
                events.Add(new SchemeEvent { Kind = SchemeEventKind.RivalLetter, Tycoon = tycoon, A = (int)r.NewsKind });
            }
            if (days >= SchemeCatalogue.ChargeAfter && r.Evidence >= SchemeCatalogue.ChargeFrom)
            {
                long found = r.Gained, taken = found + SchemeCatalogue.Share(found, SchemeCatalogue.SurchargePermille);
                r.Found = taken;
                r.Gained = 0;
                r.Acts = 0;
                r.Size = 0;
                r.Stage = InquiryStage.Charged;
                r.TrialDay = day + JusticeLaw.DaysToCourt(ChargeClass.Felony);
                RivalNews(r, RivalNewsFound, day, taken);
                events.Add(new SchemeEvent { Kind = SchemeEventKind.RivalFound, Tycoon = tycoon, A = (int)r.NewsKind, Amount = taken });
                if (r.Owes > 0)
                {
                    // A sham fund of theirs was among what was found: what is left of the player's money comes back, whatever was offered since.
                    events.Add(new SchemeEvent
                    {
                        Kind = SchemeEventKind.FundRecovered, Tycoon = tycoon, Amount = SchemeCatalogue.Share(r.Owes, SchemeCatalogue.FundRecoverPermille)
                    });
                    r.Owes = 0;
                    if (s.Fund.Tycoon == tycoon && s.Fund.State == FundState.Lost)
                    {
                        s.Fund.State = FundState.Recovered;
                    }
                }
            }
            else if (days >= SchemeCatalogue.GiveUpAfter)
            {
                r.Stage = InquiryStage.Closed;
                r.Heat = r.Heat * SchemeCatalogue.GaveUpPermille / 1000;
                events.Add(new SchemeEvent { Kind = SchemeEventKind.RivalDropped, Tycoon = tycoon });
            }
        }

        private static void RivalNews(SchemeRival r, int what, int day, long amount)
        {
            r.News = what;
            r.NewsDay = day;
            r.NewsAmount = amount;
        }

        // The fund a rival offers the player: made now and then, honest or a sham by the seed, paid or lost on its day.
        private static void FundDay(SchemeState s, int day, SchemeWorld w, List<SchemeEvent> events)
        {
            if (w.Session)
            {
                return;   // the rivals rest in a session: no fund is offered, lapses, pays or fails there
            }
            SchemeFund f = s.Fund;
            if (f.State == FundState.Open && day > f.Due)
            {
                f.State = FundState.Declined;
                events.Add(new SchemeEvent { Kind = SchemeEventKind.FundLapsed, Tycoon = f.Tycoon });
            }
            else if (f.State == FundState.Placed && day >= f.Due)
            {
                if (f.Sham)
                {
                    f.State = FundState.Lost;
                    SchemeRival r = s.Rival[SchemeCatalogue.Clamp(f.Tycoon, 0, s.Rival.Length - 1)];
                    RivalActs(s, r, f.Tycoon, SchemeKind.Club, SchemeCatalogue.Small, f.Amount, day);
                    r.Owes += f.Amount;
                    events.Add(new SchemeEvent { Kind = SchemeEventKind.FundLost, Tycoon = f.Tycoon, Amount = f.Amount });
                }
                else
                {
                    f.State = FundState.Paid;
                    events.Add(new SchemeEvent { Kind = SchemeEventKind.FundPaid, Tycoon = f.Tycoon, Amount = f.Amount, Net = FundBack(f.Amount) });
                }
            }
            bool free = f.State != FundState.Open && f.State != FundState.Placed;
            if (!free || !w.On || w.Session || day < s.StartDay + SchemeCatalogue.FundFirstDays || day - f.Day < SchemeCatalogue.FundEveryDays)
            {
                return;
            }
            int active = 0;
            for (int i = 0; i < w.RivalActive.Length; i++)
            {
                active += w.RivalActive[i] && s.Rival[i].Stage == InquiryStage.Closed && s.Rival[i].TrialDay == 0 ? 1 : 0;
            }
            if (active == 0 || w.Worth <= 0)
            {
                return;
            }
            int pick = Roll(s.Seed, DrawFundWho, f.Number, 0) % active, tycoon = -1;
            for (int i = 0; i < w.RivalActive.Length && tycoon < 0; i++)
            {
                if (w.RivalActive[i] && s.Rival[i].Stage == InquiryStage.Closed && s.Rival[i].TrialDay == 0 && pick-- == 0)
                {
                    tycoon = i;
                }
            }
            f.Tycoon = tycoon;
            f.State = FundState.Open;
            f.Day = day;
            f.Due = day + SchemeCatalogue.FundAnswerDays;
            f.Amount = FundAmount(w.Worth, w.Level);
            f.Sham = Roll(s.Seed, DrawFundSham, f.Number, tycoon) < Bent(s.Seed, tycoon) * SchemeCatalogue.FundShamShare / 1000;
            f.Checked = false;
            f.Reported = false;
            f.Number++;
            events.Add(new SchemeEvent { Kind = SchemeEventKind.FundOffered, Tycoon = tycoon, Amount = f.Amount });
        }

        /// <summary>For the tests: the wait between two offers is waived and a fund is offered now, if a rival is free to. True if one was.</summary>
        public static bool FundNow(SchemeState s, int day, SchemeWorld w, List<SchemeEvent> events)
        {
            if (s.Fund.State == FundState.Open || s.Fund.State == FundState.Placed)
            {
                return false;
            }
            s.Fund.Day = day - SchemeCatalogue.FundEveryDays;
            s.StartDay = Math.Min(s.StartDay, day - SchemeCatalogue.FundFirstDays);
            FundDay(s, day, w, events);
            return s.Fund.State == FundState.Open;
        }

        /// <summary>For the tests: a rival has a full-sized scheme on file and its inquiry makes its findings today. False for a rival already before the court.</summary>
        public static bool RivalNow(SchemeState s, int tycoon, int day, SchemeWorld w, List<SchemeEvent> events)
        {
            if (tycoon < 0 || tycoon >= s.Rival.Length || s.Rival[tycoon].TrialDay > 0)
            {
                return false;
            }
            SchemeRival r = s.Rival[tycoon];
            long gain = RivalGain(w.RivalWorth[tycoon], SchemeCatalogue.Large, w.Level);
            RivalActs(s, r, tycoon, SchemeKind.Claim, SchemeCatalogue.Large, gain, day);
            events.Add(new SchemeEvent { Kind = SchemeEventKind.RivalActed, Tycoon = tycoon, A = (int)SchemeKind.Claim, Amount = gain });
            r.Stage = InquiryStage.Letter;
            r.Since = day - SchemeCatalogue.ChargeAfter;
            r.Evidence = SchemeCatalogue.EvidenceMost;
            r.Opened++;
            RivalInquiry(s, r, tycoon, day, w, events);
            return true;
        }

        /// <summary>True while the fund on offer can still be answered today.</summary>
        public static bool FundStands(SchemeState s, int day)
        {
            return s.Fund.State == FundState.Open && day <= s.Fund.Due;
        }

        /// <summary>The player puts the money in: true if the offer stood. The caller has paid the sum first (asking <see cref="FundStands"/> before it).</summary>
        public static bool FundPlace(SchemeState s, int day)
        {
            SchemeFund f = s.Fund;
            if (!FundStands(s, day))
            {
                return false;
            }
            f.State = FundState.Placed;
            f.Due = day + SchemeCatalogue.FundDays;
            return true;
        }

        /// <summary>The player has it looked at: what it is, is known from here on. True if there was an open offer not yet looked at.</summary>
        public static bool FundCheck(SchemeState s)
        {
            if (s.Fund.State != FundState.Open || s.Fund.Checked)
            {
                return false;
            }
            s.Fund.Checked = true;
            return true;
        }

        public static bool FundDecline(SchemeState s)
        {
            if (s.Fund.State != FundState.Open)
            {
                return false;
            }
            s.Fund.State = FundState.Declined;
            return true;
        }

        /// <summary>The player reports a sham to the police: once, it falls on the rival's trail as an offer reported does.</summary>
        public static bool FundReport(SchemeState s)
        {
            SchemeFund f = s.Fund;
            if (f.State != FundState.Lost || f.Reported || f.Tycoon < 0 || f.Tycoon >= s.Rival.Length)
            {
                return false;
            }
            f.Reported = true;
            s.Rival[f.Tycoon].Heat = Math.Min(SchemeCatalogue.HeatMost, s.Rival[f.Tycoon].Heat + SchemeCatalogue.HeatReported);
            return true;
        }
    }
}
