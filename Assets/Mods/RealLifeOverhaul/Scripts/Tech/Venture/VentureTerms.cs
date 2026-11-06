using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Tech
{
    /// <summary>
    /// What a venture firm asks beyond the money: the terms of a round. A line of a cap table with none (null) is the plain
    /// deal every cap had before terms: one times, not participating, no anti-dilution, the vesting it had, a monthly pack, no provision. Game-free.
    /// </summary>
    public sealed class Terms
    {
        public int Pref = 1000;            // thousandths of the money put in that comes back first on a sale: 1000, 1500 or 2000
        public bool Participating;         // takes the preference and then shares as common too
        public int Anti;                   // when a later round prices lower: 0 none, 1 broad weighted average, 2 full ratchet
        public int Vest;                   // 0 keeps the founder's vesting as it is, 1 the round starts it again
        public int Info;                   // 0 a monthly pack, 1 a quarterly pack, 2 a monthly pack and a look on demand
        public long Provision;             // cents at the start's prices: spending above this in one go needs the board; 0 none

        public bool Standard => Pref == 1000 && !Participating && Anti == 0 && Vest == 0 && Info == 0 && Provision == 0;

        public Terms Copy() => new Terms { Pref = Pref, Participating = Participating, Anti = Anti, Vest = Vest, Info = Info, Provision = Provision };
    }

    /// <summary>The board's say over a firm, kept beside its cap table: the provision's approvals and breaches, the salary vote, and a pack's slip. Game-free.</summary>
    public sealed class CapRights
    {
        public int Breaches, BreachDay;      // spending over the provision without the board; counted twice after a refusal
        public long Approved;                // cents at the start's prices the board has agreed a big spend for
        public int ApprovedUntil;            // the last day that approval stands
        public int RefusedUntil;             // the board will not be asked again before this day
        public int RaiseStage = -2;          // the stage at which the board was asked about a salary over the band
        public int SlipDay;                  // the day of the last pack with an honest slip in it; 0 none
    }

    /// <summary>The figures of terms. Cents at the start's prices.</summary>
    public static class TermsCatalogue
    {
        public const int TPref = 0, TPart = 1, TAnti = 2, TVest = 3, TInfo = 4, TProv = 5, Count = 6;
        public const int InfoMonthly = 0, InfoQuarterly = 1, InfoDemand = 2;
        public const int AntiNone = 0, AntiBroad = 1, AntiRatchet = 2;
        public const int QuarterDays = 90;                // the pack of a quarterly right
        public const int ApproveDays = 14, ApproveTimes = 4;   // a big spend agreed stands this long, and covers this many provisions
        public const int RefuseDays = 14;                 // the board will not hear the ask again for this long after a refusal
        public const int BreachDays = 120;                // a breach weighs on suspicion this long
        public const int BreachWeight = 8, SlipWeight = 5;   // suspicion points a breach, and a slip in a pack
        public const int SlipDays = 60;
        public const int RaisePercent = 150;              // a salary band the board lifts to, percent
        public const int SoftenDays = 3;                  // a push that fails shortens the offer to this many days
        public const int SourPercent = 4;                 // a push that fails badly takes this much off the offer's value
        public const int DownCut = 400;                   // thousandths a later round prices lower by, in the costs the Law page works
        public const int HardPercent = 5;                 // a term costing this much of the player's money in a sale is hard
        public const int LawToRead = 40;                  // the Law the costs and the hard terms need
        public const int AnalysisToSee = 35;              // Analysis or Statistics needed to see an offer against the market
        public const int GrowPitch = 600, GrowBoard = 450, GrowPush = 450, GrowRead = 300, GrowPack = 150;   // thousandths of a point, at no skill
        public static readonly int[] PrefSteps = { 1000, 1500, 2000 };
        public static readonly string[] PrefKeys = { "reallife_vc_terms_pref_1", "reallife_vc_terms_pref_15", "reallife_vc_terms_pref_2" };
        public static readonly string[] AntiKeys = { "reallife_vc_terms_anti_0", "reallife_vc_terms_anti_1", "reallife_vc_terms_anti_2" };
        public static readonly string[] VestKeys = { "reallife_vc_terms_vest_0", "reallife_vc_terms_vest_1" };
        public static readonly string[] InfoKeys = { "reallife_vc_terms_info_0", "reallife_vc_terms_info_1", "reallife_vc_terms_info_2" };
        public static readonly string[] PartKeys = { "reallife_vc_terms_part_0", "reallife_vc_terms_part_1" };
        public static readonly string[] LabelKeys =
        {
            "reallife_vc_terms_label_pref", "reallife_vc_terms_label_part", "reallife_vc_terms_label_anti", "reallife_vc_terms_label_vest", "reallife_vc_terms_label_info",
            "reallife_vc_terms_label_prov"
        };
        public static readonly string[] PushKeys =
        {
            "reallife_vc_push_pref", "reallife_vc_push_part", "reallife_vc_push_anti", "reallife_vc_push_vest", "reallife_vc_push_info", "reallife_vc_push_prov"
        };
    }

    /// <summary>What a Law-reading of an offer shows: the player's loss in a sale from each term, worked by the waterfall. Cents at the start's prices.</summary>
    public sealed class TermsCosts
    {
        public long Value, Value3;                 // the sale prices the costs are worked at: the round's value after the money, and three times it
        public long Plain, Plain3;                 // what the player's lines get with plain terms (one times, not participating)
        public long Pref, Pref3;                   // the cost of the multiple
        public long Part, Part3;                   // the cost of participating, on top of the multiple
        public long Total, Total3;                 // all of it: plain less what the offer pays
        public int ShareDown, ShareDownAnti;       // the player's share, thousandths, after a later round priced lower: without and with the anti-dilution
        public bool HardPref, HardPart, HardAnti, HardVest, HardInfo, HardProv;

        public bool Hard(int term)
        {
            switch (term)
            {
                case TermsCatalogue.TPref: return HardPref;
                case TermsCatalogue.TPart: return HardPart;
                case TermsCatalogue.TAnti: return HardAnti;
                case TermsCatalogue.TVest: return HardVest;
                case TermsCatalogue.TInfo: return HardInfo;
                default: return HardProv;
            }
        }
    }

    /// <summary>
    /// The rules of terms: how a firm sets them, what they do to a sale, a later round and the board, and how the player pushes on them.
    /// Game-free; every roll is <see cref="TechRandom"/>'s.
    /// </summary>
    public static class VentureTerms
    {
        public static Terms Of(Holder h) => h.T ?? new Terms();

        // ---------------------------------------------------------------- how a firm sets its terms

        /// <summary>
        /// How hard a firm asks, 0 to 1000: its diligence above all, a poor market, a firm in trouble, a later stage and a little of the day's dice.
        /// </summary>
        public static int Strictness(int diligence, int stage, int mood, int weak, int roll)
        {
            int s = TalentCatalogue.Clamp(diligence, 0, 1000) * 55 / 100;
            s += TalentCatalogue.Clamp(1000 - mood, -250, 440) * 3 / 10;
            s += TalentCatalogue.Clamp(weak, 0, 1000) * 15 / 100;
            s += TalentCatalogue.Clamp(stage, 0, (int)RoundStage.Count - 1) * 20;
            s += (TalentCatalogue.Clamp(roll, 0, 999) - 500) / 6;
            return TalentCatalogue.Clamp(s, 0, 1000);
        }

        /// <summary>
        /// The terms a firm asks at a stage. <paramref name="weak"/> is how badly the firm is doing, 0 to 1000; <paramref name="key"/> and <paramref name="day"/>
        /// say what the roll is about.
        /// </summary>
        public static Terms Draw(int vc, int stage, int mood, int weak, int seed, int key, int day)
        {
            VcDef def = VentureCatalogue.Firms[TalentCatalogue.Clamp(vc, 0, VentureCatalogue.FirmCount - 1)];
            int stageNow = TalentCatalogue.Clamp(stage, 0, (int)RoundStage.Count - 1);
            int s = Strictness(def.Diligence, stageNow, mood, weak, TechRandom.Mille(seed, key, day, 301));
            var t = new Terms();
            t.Pref = s >= 720 ? 2000 : s >= 520 ? 1500 : 1000;
            t.Participating = s >= 640 && TechRandom.Mille(seed, key, day, 302) < s - 400;
            t.Anti = s >= 760 ? (TechRandom.Mille(seed, key, day, 303) < 400 ? TermsCatalogue.AntiRatchet : TermsCatalogue.AntiBroad) : s >= 380 ? TermsCatalogue.AntiBroad : TermsCatalogue.AntiNone;
            t.Vest = stageNow >= 1 && s >= 600 && TechRandom.Mille(seed, key, day, 304) < s - 300 ? 1 : 0;
            t.Info = s >= 700 ? TermsCatalogue.InfoDemand : s >= 330 ? TermsCatalogue.InfoMonthly : TermsCatalogue.InfoQuarterly;
            if (s >= 350)
            {
                int months = Math.Max(300, 3200 - s * 3);   // thousandths of a month of the stage's burn
                t.Provision = VentureCatalogue.BurnByStage[stageNow] * 30 * months / 1000;
            }
            return t;
        }

        /// <summary>The terms of a line, or null when they are the plain ones (so the cap table of an old save and a plain round look the same).</summary>
        public static Terms Stamp(int vc, int stage, int mood, int weak, int seed, int key, int day)
        {
            Terms t = Draw(vc, stage, mood, weak, seed, key, day);
            return t.Standard ? null : t;
        }

        /// <summary>How badly a city startup is doing, 0 to 1000: little runway and weak founders.</summary>
        public static int Weak(Startup s)
        {
            int runway = VentureRules.Runway(s.Cash, s.Burn, s.Mrr);
            int w = (180 - Math.Min(180, runway)) * 4 + (1000 - TalentCatalogue.Clamp(s.Quality, 0, 1000)) / 4;
            return TalentCatalogue.Clamp(w, 0, 1000);
        }

        // ---------------------------------------------------------------- a later round that prices lower

        /// <summary>
        /// A round priced at <paramref name="pre"/> before <paramref name="money"/> comes in, with <paramref name="total"/> shares out before it: every line with
        /// anti-dilution that paid more a share is given more shares, by the broad weighted average or the full ratchet. Returns the shares made.
        /// </summary>
        public static long AntiDilute(IList<Holder> holders, long total, long pre, long money)
        {
            if (total <= 0 || pre <= 0 || money <= 0)
            {
                return 0L;
            }
            double newPrice = (double)pre / total, made = 0;
            for (int i = 0; i < holders.Count; i++)
            {
                Holder h = holders[i];
                if (h.T == null || h.T.Anti == TermsCatalogue.AntiNone || h.Shares <= 0 || h.Invested <= 0)
                {
                    continue;
                }
                double oldPrice = (double)h.Invested / h.Shares;
                if (oldPrice <= newPrice)
                {
                    continue;
                }
                double price = newPrice;
                if (h.T.Anti == TermsCatalogue.AntiBroad)
                {
                    double issuedAtOld = money / oldPrice, issuedAtNew = money / newPrice;
                    price = oldPrice * (total + issuedAtOld) / (total + issuedAtNew);
                }
                long add = (long)(h.Invested / price) - h.Shares;
                if (add > 0)
                {
                    h.Shares += add;
                    made += add;
                }
            }
            return (long)made;
        }

        // ---------------------------------------------------------------- what the terms do to the board

        /// <summary>The books the lead sees: the strictest right any investor holds (a plain line is a monthly pack).</summary>
        public static int InfoOf(Cap c)
        {
            bool any = false, monthly = false;
            for (int i = 0; i < c.Holders.Count; i++)
            {
                Holder h = c.Holders[i];
                if (h.Kind != (int)HolderKind.Vc)
                {
                    continue;
                }
                any = true;
                int info = h.T != null ? h.T.Info : TermsCatalogue.InfoMonthly;
                if (info == TermsCatalogue.InfoDemand)
                {
                    return TermsCatalogue.InfoDemand;
                }
                monthly |= info == TermsCatalogue.InfoMonthly;
            }
            return any && !monthly ? TermsCatalogue.InfoQuarterly : TermsCatalogue.InfoMonthly;
        }

        /// <summary>Days between board packs.</summary>
        public static int PackEvery(Cap c) => InfoOf(c) == TermsCatalogue.InfoQuarterly ? TermsCatalogue.QuarterDays : MoneyCatalogue.PackDays;

        /// <summary>The lead may look on any day, not only on a pack day.</summary>
        public static bool OnDemand(Cap c) => InfoOf(c) == TermsCatalogue.InfoDemand;

        /// <summary>Points off the suspicion at which the lead looks: a right to look on demand makes it sooner, a quarterly pack later.</summary>
        public static int ThresholdShift(Cap c)
        {
            int info = InfoOf(c);
            return info == TermsCatalogue.InfoDemand ? -5 : info == TermsCatalogue.InfoQuarterly ? 4 : 0;
        }

        /// <summary>Thousandths more (or fewer) of what was taken that a plain look finds.</summary>
        public static int LookShift(Cap c)
        {
            int info = InfoOf(c);
            return info == TermsCatalogue.InfoDemand ? 100 : info == TermsCatalogue.InfoQuarterly ? -80 : 0;
        }

        /// <summary>The smallest provision any investor holds, cents at the start's prices; 0 none.</summary>
        public static long ProvisionOf(Cap c)
        {
            long least = 0L;
            for (int i = 0; i < c.Holders.Count; i++)
            {
                Holder h = c.Holders[i];
                if (h.Kind == (int)HolderKind.Vc && h.T != null && h.T.Provision > 0 && (least == 0 || h.T.Provision < least))
                {
                    least = h.T.Provision;
                }
            }
            return least;
        }

        public static bool Approved(Cap c, long sum, int day) => c.Rt.ApprovedUntil >= day && c.Rt.Approved >= sum;

        /// <summary>A spend over the provision, cents at the start's prices: covered by an approval (which it uses up), or a breach. True if it was covered or needs no board.</summary>
        public static bool Spend(Cap c, long sum, int day)
        {
            long prov = ProvisionOf(c);
            if (prov <= 0 || sum <= prov)
            {
                return true;
            }
            if (Approved(c, sum, day))
            {
                c.Rt.Approved -= sum;
                return true;
            }
            return false;
        }

        /// <summary>Spending over the provision with the board not asked, or refused: a breach that weighs on suspicion, twice as much after a refusal.</summary>
        public static void Breach(Cap c, int day)
        {
            c.Rt.Breaches += day < c.Rt.RefusedUntil ? 2 : 1;
            c.Rt.BreachDay = day;
            c.Acc.Event = day;
            c.Acc.Calm = 0;
        }

        /// <summary>Suspicion points from breaches of the provision and a slip in a pack, which fade with time.</summary>
        public static int Extra(Cap c, int day)
        {
            CapRights r = c.Rt;
            int breach = r.Breaches > 0 && day - r.BreachDay < TermsCatalogue.BreachDays ? Math.Min(24, r.Breaches * TermsCatalogue.BreachWeight) : 0;
            int slip = r.SlipDay > 0 && day - r.SlipDay < TermsCatalogue.SlipDays ? TermsCatalogue.SlipWeight : 0;
            return breach + slip;
        }

        // ---------------------------------------------------------------- the player's skills

        /// <summary>
        /// The chance, thousandths, that the board agrees to a big spend or a salary over the band: stricter leads and a missed target lower it, Leadership
        /// raises it; never when money is counted as taken.
        /// </summary>
        public static int VoteChance(Cap c, int leadership, bool salary)
        {
            if (c.Acc.Taken > 0 || c.Acc.Step > StartupMoney.StepNone)
            {
                return 0;
            }
            int chance = (salary ? 450 : 600) + TalentCatalogue.Clamp(leadership, 0, 100) * 3 - StartupMoney.Diligence(c) * 3 / 10 - (c.Misses + c.Pressure) * 150;
            return TalentCatalogue.Clamp(chance, 50, 950);
        }

        /// <summary>The honest slip in a board pack: more likely with less Accounting, never below a few in a hundred.</summary>
        public static int SlipChance(int accounting) => Math.Max(20, 260 - TalentCatalogue.Clamp(accounting, 0, 100) * 25 / 10);

        /// <summary>How warm a firm gets in a pitch, points added to the meeting's score: Selling and Relationships.</summary>
        public static int Warmth(int selling, int relationships) => (TalentCatalogue.Clamp(selling, 0, 100) + TalentCatalogue.Clamp(relationships, 0, 100)) * 3 / 10;

        /// <summary>The chance, thousandths, that a firm gives way on a term: Law, Selling and Relationships help, a warm meeting too, a strict firm and a poor market do not.</summary>
        public static int PushChance(int law, int selling, int relationships, int score, int diligence, int mood)
        {
            int chance = 250 + TalentCatalogue.Clamp(law, 0, 100) * 2 + TalentCatalogue.Clamp(selling, 0, 100) * 2 + TalentCatalogue.Clamp(relationships, 0, 100)
                         + (TalentCatalogue.Clamp(score, 0, 1000) - 500) / 5 - TalentCatalogue.Clamp(diligence, 0, 1000) / 8 + (mood - 1000) / 8;
            return TalentCatalogue.Clamp(chance, 60, 880);
        }

        /// <summary>The term on offer is harder than the plain one, so it can be pushed on.</summary>
        public static bool Pushable(Terms t, int term)
        {
            switch (term)
            {
                case TermsCatalogue.TPref: return t.Pref > 1000;
                case TermsCatalogue.TPart: return t.Participating;
                case TermsCatalogue.TAnti: return t.Anti > 0;
                case TermsCatalogue.TVest: return t.Vest > 0;
                case TermsCatalogue.TInfo: return t.Info == TermsCatalogue.InfoDemand;
                default: return t.Provision > 0;
            }
        }

        /// <summary>A firm gives way on a term by one step.</summary>
        public static void Relax(Terms t, int term)
        {
            switch (term)
            {
                case TermsCatalogue.TPref: t.Pref = t.Pref >= 2000 ? 1500 : 1000; break;
                case TermsCatalogue.TPart: t.Participating = false; break;
                case TermsCatalogue.TAnti: t.Anti = Math.Max(0, t.Anti - 1); break;
                case TermsCatalogue.TVest: t.Vest = 0; break;
                case TermsCatalogue.TInfo: t.Info = TermsCatalogue.InfoMonthly; break;
                default: t.Provision = t.Provision * 2; break;
            }
        }

        /// <summary>A deck's revenue stretched far enough for the player's eye: Accounting, or Analysis and Statistics together, against how far it was stretched (basis points).</summary>
        public static bool Stretched(int accounting, int analysis, int statistics, int claimBp)
        {
            int best = Math.Max(TalentCatalogue.Clamp(accounting, 0, 100), (TalentCatalogue.Clamp(analysis, 0, 100) + TalentCatalogue.Clamp(statistics, 0, 100)) / 2);
            return claimBp >= Math.Max(500, 1800 - best * 14);
        }

        // ---------------------------------------------------------------- what the terms cost in a sale

        private static List<Holder> Clone(IList<Holder> holders, Terms newInvestor, int vc, long amount, long pre, int round)
        {
            long total = 0;
            var list = new List<Holder>(holders.Count + 1);
            for (int i = 0; i < holders.Count; i++)
            {
                Holder h = holders[i];
                total += h.Shares;
                list.Add(new Holder { Kind = h.Kind, Who = h.Who, Shares = h.Shares, Invested = h.Invested, Preference = h.Preference, Round = h.Round, T = h.T });
            }
            var n = new Holder { Kind = (int)HolderKind.Vc, Who = vc, Invested = amount, Round = round, T = newInvestor };
            n.Shares = (long)((double)amount * Math.Max(1, total) / Math.Max(1, pre));
            n.Preference = amount * newInvestor.Pref / 1000;
            list.Add(n);
            return list;
        }

        private static long Mine(IList<Holder> holders, long price)
        {
            long[] pay = VentureRules.Waterfall(holders, price);
            long sum = 0;
            for (int i = 0; i < pay.Length; i++)
            {
                sum += holders[i].Players ? pay[i] : 0L;
            }
            return sum;
        }

        private static int MineShare(IList<Holder> holders)
        {
            long mine = 0, total = 0;
            for (int i = 0; i < holders.Count; i++)
            {
                total += holders[i].Shares;
                mine += holders[i].Players ? holders[i].Shares : 0L;
            }
            return total > 0 ? (int)(mine * 1000 / total) : 0;
        }

        /// <summary>
        /// What the offered terms would cost the player in a sale at the round's value and at three times it, one term at a time, each worked by
        /// <see cref="VentureRules.Waterfall"/> on the cap table with the new investor in it; the anti-dilution at a later round priced a set cut lower.
        /// </summary>
        public static TermsCosts Cost(IList<Holder> current, Terms offer, int vc, long amount, long pre, int round, long monthBurn)
        {
            Terms t = offer ?? new Terms();
            var plain = new Terms();
            var multiple = new Terms { Pref = t.Pref };
            var both = new Terms { Pref = t.Pref, Participating = t.Participating };
            long value = pre + amount, value3 = value * 3;
            var r = new TermsCosts { Value = value, Value3 = value3 };
            List<Holder> a = Clone(current, plain, vc, amount, pre, round), b = Clone(current, multiple, vc, amount, pre, round), c = Clone(current, both, vc, amount, pre, round);
            r.Plain = Mine(a, value);
            r.Plain3 = Mine(a, value3);
            long m1 = Mine(b, value), m3 = Mine(b, value3), p1 = Mine(c, value), p3 = Mine(c, value3);
            r.Pref = Math.Max(0L, r.Plain - m1);
            r.Pref3 = Math.Max(0L, r.Plain3 - m3);
            r.Part = Math.Max(0L, m1 - p1);
            r.Part3 = Math.Max(0L, m3 - p3);
            r.Total = r.Pref + r.Part;
            r.Total3 = r.Pref3 + r.Part3;
            // a later round priced a cut lower, with and without the anti-dilution
            long lower = value * (1000 - TermsCatalogue.DownCut) / 1000, more = amount / 2;
            List<Holder> withA = Clone(current, new Terms { Anti = t.Anti }, vc, amount, pre, round), withoutA = Clone(current, plain, vc, amount, pre, round);
            r.ShareDown = Down(withoutA, lower, more);
            r.ShareDownAnti = Down(withA, lower, more);
            r.HardPref = t.Pref >= 2000 || (r.Plain > 0 && (r.Pref * 100 >= r.Plain * TermsCatalogue.HardPercent || r.Pref3 * 100 >= r.Plain3 * TermsCatalogue.HardPercent));
            r.HardPart = t.Participating && (r.Plain > 0 && (r.Part * 100 >= r.Plain * TermsCatalogue.HardPercent || r.Part3 * 100 >= r.Plain3 * TermsCatalogue.HardPercent));
            r.HardAnti = t.Anti == TermsCatalogue.AntiRatchet || (t.Anti > 0 && r.ShareDown - r.ShareDownAnti >= 20);
            r.HardVest = t.Vest > 0;
            r.HardInfo = t.Info == TermsCatalogue.InfoDemand;
            r.HardProv = t.Provision > 0 && t.Provision < monthBurn;
            return r;
        }

        // The player's share after a later round of <more> at <pre>, the new investor on plain terms.
        private static int Down(List<Holder> holders, long pre, long more)
        {
            long total = 0;
            for (int i = 0; i < holders.Count; i++)
            {
                total += holders[i].Shares;
            }
            var n = new Holder { Kind = (int)HolderKind.Vc, Invested = more, Shares = (long)((double)more * Math.Max(1, total) / Math.Max(1, pre)) };
            holders.Add(n);
            AntiDilute(holders, total, pre, more);
            return MineShare(holders);
        }
    }
}
