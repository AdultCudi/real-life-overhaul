using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Tech
{
    /// <summary>
    /// The rules of startups and venture capital; the player's own firms raising are in VentureRules.Caps. Game-free; every roll
    /// is <see cref="TechRandom"/>'s.
    /// </summary>
    public static partial class VentureRules
    {
        public const int NoteFounded = 1, NoteRaised = 2, NoteDown = 3, NoteFailed = 4, NoteAcquired = 5, NotePitch = 6, NoteFollowOn = 7, NoteListing = 8,
            NoteAcquihire = 9;
        public const long FounderShares = 10000000;   // shares the founders start with

        /// <summary>The market's mood for startups, thousandths: the city's cycle.</summary>
        public static int Mood(int cycle) => VentureCatalogue.MoodByCycle[TalentCatalogue.Clamp(cycle, 0, 3)];

        /// <summary>The AI wave, thousandths: rising and falling over a couple of the game's years.</summary>
        public static int Hype(int seed, int day, int year)
        {
            double period = Math.Max(60, year) * VentureCatalogue.HypeYears;
            double phase = (TechRandom.Mille(seed, 1, 2) / 1000.0) * 2 * Math.PI;
            return 1000 + (int)(VentureCatalogue.HypeSwing * Math.Sin(2 * Math.PI * day / period + phase));
        }

        /// <summary>A month's growth of a startup's revenue, thousandths: by its founders, the mood, and for an AI company the wave.</summary>
        public static int Growth(Startup s, int mood, int hype)
        {
            long g = VentureCatalogue.GrowthWorst + (long)(VentureCatalogue.GrowthBest - VentureCatalogue.GrowthWorst) * s.Quality / 1000;
            g = g * mood / 1000;
            if (s.Sector == (int)Venture.Ai)
            {
                g = g * hype / 1000;
            }
            return (int)Math.Max(0, g);
        }

        /// <summary>A new startup: founders' quality, sector (AI more often on the wave), a little revenue, six months of money and the founders' shares.</summary>
        public static Startup Born(VentureFile f, int day, int hype)
        {
            int id = f.Next();
            var s = new Startup { Id = id, Founded = day, State = (int)StartupState.Running, Stage = (int)RoundStage.PreSeed };
            s.Quality = TechRandom.Range(f.Seed, id, 1, 0, 100, 950);
            s.Sector = TechRandom.Mille(f.Seed, id, 2) < 120 * hype / 1000 ? (int)Venture.Ai : TechRandom.Range(f.Seed, id, 3, 0, 0, (int)Venture.Count - 1);
            s.Mrr = TechRandom.Range(f.Seed, id, 4, 0, 0, 300000);
            s.ClaimBp = TechRandom.Mille(f.Seed, id, 5) < 500 ? 0 : TechRandom.Range(f.Seed, id, 6, 0, 500, 6000);
            s.Burn = VentureCatalogue.BurnByStage[0];
            s.Cash = s.Burn * 30 * 6;
            s.Post = VentureCatalogue.FloorValue[0];
            s.Name = VentureCatalogue.NameA[TechRandom.Range(f.Seed, id, 7, 0, 0, VentureCatalogue.NameA.Length - 1)]
                     + VentureCatalogue.NameB[TechRandom.Range(f.Seed, id, 8, 0, 0, VentureCatalogue.NameB.Length - 1)];
            s.Holders.Add(new Holder { Kind = (int)HolderKind.Founders, Shares = FounderShares });
            return s;
        }

        /// <summary>The days of money a startup has left at its burn, less what its revenue brings in.</summary>
        public static int Runway(long cash, long burn, long mrr)
        {
            long net = burn - mrr / 30;
            if (net <= 0)
            {
                return int.MaxValue;
            }
            return (int)Math.Max(0, Math.Min(int.MaxValue, cash / net));
        }

        /// <summary>A round's value before the money, cents: a multiple of a year's recurring revenue by growth and mood, never under the stage's floor.</summary>
        public static long PreMoney(long mrr, int growth, int mood, int stage)
        {
            int span = VentureCatalogue.GrowthBest - VentureCatalogue.GrowthWorst;
            long multiple = VentureCatalogue.MultipleLeast * 1000L
                            + (long)(VentureCatalogue.MultipleMost - VentureCatalogue.MultipleLeast) * 1000 * TalentCatalogue.Clamp(growth - VentureCatalogue.GrowthWorst, 0, span) / span;
            multiple = multiple * mood / 1000;
            long byRevenue = mrr * 12 * multiple / 1000;
            return Math.Max(VentureCatalogue.FloorValue[TalentCatalogue.Clamp(stage, 0, (int)RoundStage.Count - 1)], byRevenue);
        }

        /// <summary>The size of the next round, cents: so many months of the next stage's burn.</summary>
        public static long RoundSize(int nextStage)
        {
            return (long)VentureCatalogue.BurnByStage[TalentCatalogue.Clamp(nextStage, 0, (int)RoundStage.Count - 1)] * 30 * VentureCatalogue.RunwayMonthsRaised;
        }

        /// <summary>The lead for a round: a firm that leads at the stage, one keen on the sector first; -1 when the mood brings nobody.</summary>
        public static int Lead(VentureFile f, Startup s, int stage, int mood, int day)
        {
            if (TechRandom.Mille(f.Seed, s.Id, day, 21) >= 300 + mood / 2)
            {
                return -1;
            }
            int best = -1, bestScore = -1;
            for (int i = 0; i < VentureCatalogue.Firms.Length; i++)
            {
                VcDef vc = VentureCatalogue.Firms[i];
                if (stage < vc.Least || stage > vc.Most)
                {
                    continue;
                }
                int score = ((vc.Likes & (1 << s.Sector)) != 0 ? 500 : 0) + TechRandom.Mille(f.Seed, s.Id, day, 22 + i) / 2;
                if (score > bestScore)
                {
                    bestScore = score;
                    best = i;
                }
            }
            return best;
        }

        /// <summary>
        /// A round closes: new shares at the pre-money's price, a staff pool at the seed round; the stage moves on. The player's
        /// shares before it are kept, so their one follow-on of it can bring them back to that share and no further.
        /// </summary>
        public static void Round(Startup s, long pre, IList<Holder> investors, int day)
        {
            s.BeforeMine = PlayerShares(s);
            s.BeforeTotal = s.Total;
            s.RoundDay = day;
            s.Followed = false;
            long total = Math.Max(1, s.Total);
            long raised = 0;
            for (int i = 0; i < investors.Count; i++)
            {
                Holder h = investors[i];
                h.Shares = (long)((double)h.Invested * total / Math.Max(1, pre));
                h.Preference = h.Invested * VentureCatalogue.LiquidationPreference / 1000;
                h.Round = s.Stage + 1;
                s.Holders.Add(h);
                raised += h.Invested;
            }
            s.Stage = Math.Min((int)RoundStage.Growth, s.Stage + 1);
            if (s.Stage == (int)RoundStage.Seed && !HasPool(s.Holders))
            {
                long pool = s.Total * VentureCatalogue.OptionPool / 1000;
                s.Holders.Add(new Holder { Kind = (int)HolderKind.Pool, Shares = pool });
            }
            s.Post = pre + raised;
            s.Cash += raised;
            s.Burn = VentureCatalogue.BurnByStage[s.Stage];
            s.LastRound = day;
            s.FailedRaises = 0;
            s.State = (int)StartupState.Running;
            s.Ask = null;
        }

        private static bool HasPool(List<Holder> holders)
        {
            for (int i = 0; i < holders.Count; i++)
            {
                if (holders[i].Kind == (int)HolderKind.Pool)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>A holder's share of the company, thousandths.</summary>
        public static int ShareOf(Holder h, long total) => total > 0 ? (int)(h.Shares * 1000 / total) : 0;

        /// <summary>A sale's money by the cap table, preferences first (the latest round first). Returns each line's money in order.</summary>
        public static long[] Waterfall(IList<Holder> holders, long price)
        {
            var pay = new long[holders.Count];
            long total = 0;
            for (int i = 0; i < holders.Count; i++)
            {
                total += holders[i].Shares;
            }
            if (total <= 0 || price <= 0)
            {
                return pay;
            }
            var prefers = new bool[holders.Count];
            long left = price, sharesLeft = total;
            // who takes the preference, worked out again until nobody changes their mind
            for (int pass = 0; pass < holders.Count + 1; pass++)
            {
                bool changed = false;
                long prefTotal = 0, converted = 0;
                for (int i = 0; i < holders.Count; i++)
                {
                    if (prefers[i])
                    {
                        prefTotal += holders[i].Preference;
                    }
                    else
                    {
                        converted += holders[i].Shares;
                    }
                }
                long rest = Math.Max(0, price - prefTotal);
                for (int i = 0; i < holders.Count; i++)
                {
                    Holder h = holders[i];
                    if (h.Preference <= 0)
                    {
                        continue;
                    }
                    long asCommon = converted + (prefers[i] ? h.Shares : 0) > 0 ? (long)((double)rest * h.Shares / (converted + (prefers[i] ? h.Shares : 0))) : 0;
                    bool want = h.Preference > asCommon;
                    if (want != prefers[i])
                    {
                        prefers[i] = want;
                        changed = true;
                        break;
                    }
                }
                if (!changed)
                {
                    break;
                }
            }
            for (int round = (int)RoundStage.Count; round >= 0; round--)
            {
                for (int i = 0; i < holders.Count; i++)
                {
                    if (prefers[i] && holders[i].Round == round)
                    {
                        long take = Math.Min(left, holders[i].Preference);
                        pay[i] = take;
                        left -= take;
                        sharesLeft -= holders[i].Shares;
                    }
                }
            }
            if (left > 0 && sharesLeft > 0)
            {
                long given = 0;
                int last = -1;
                for (int i = 0; i < holders.Count; i++)
                {
                    if (!prefers[i])
                    {
                        pay[i] = (long)((double)left * holders[i].Shares / sharesLeft);
                        given += pay[i];
                        last = i;
                    }
                }
                if (last >= 0)
                {
                    pay[last] += left - given;   // what rounding left over
                }
            }
            return pay;
        }

        /// <summary>A day of a city startup: growth and burn, raising, failing, being bought or listing.</summary>
        public static void Day(VentureFile f, Startup s, int day, int cycle, int hype, bool pitchPlayer, List<VentureNote> notes)
        {
            if (!s.Alive)
            {
                return;
            }
            int mood = Mood(cycle);
            int growth = Growth(s, mood, hype);
            double daily = Math.Pow(1.0 + growth / 1000.0, 1.0 / 30.0) - 1.0;
            if (s.Mrr == 0)
            {
                s.Mrr = 10000L * s.Quality / 1000;   // first customers come
            }
            double grown = s.Mrr * daily;
            long whole = (long)grown;
            s.Carry += (long)((grown - whole) * 1e6);
            whole += s.Carry / 1000000;
            s.Carry %= 1000000;
            s.Mrr += whole;
            s.Cash += s.Mrr / 30 - s.Burn;
            int runway = Runway(s.Cash, s.Burn, s.Mrr);
            if (s.State == (int)StartupState.Running && runway < VentureCatalogue.RaiseWhenMonths * 30 && s.Stage < (int)RoundStage.Growth)
            {
                s.State = (int)StartupState.Raising;
                s.LastRound = day;
                if (pitchPlayer && TechRandom.Mille(f.Seed, s.Id, day, 31) < VentureCatalogue.PitchChance)
                {
                    long round = RoundSize(s.Stage + 1);
                    s.Ask = new TermSheet
                    {
                        Amount = Math.Max(VentureCatalogue.TicketLeast, round * VentureCatalogue.AskPlayerShare / 1000), Pre = PreMoney(s.Mrr, growth, mood, s.Stage + 1),
                        Lapse = day + 7, ProRata = true
                    };
                    notes?.Add(new VentureNote { Kind = NotePitch, Startup = s.Id, Amount = s.Ask.Amount });
                }
            }
            else if (s.State == (int)StartupState.Raising && day - s.LastRound >= 10)
            {
                Close(f, s, day, mood, growth, notes);
            }
            if (s.Alive && s.Cash < 0)
            {
                if (s.FailedRaises >= VentureCatalogue.FailedRaisesToDie)
                {
                    bool acquihire = s.Stage >= (int)RoundStage.Seed && TechRandom.Mille(f.Seed, s.Id, day, 41) < 400;
                    long price = acquihire ? Invested(s) * VentureCatalogue.AcquihireShare / 1000 : 0;
                    Sell(f, s, price, acquihire ? NoteAcquihire : NoteFailed, notes);
                    return;
                }
                s.Cash = 0;   // the founders go without pay until the raise
            }
            if (s.Alive && s.Stage >= (int)RoundStage.Seed && TechRandom.Mille(f.Seed, s.Id, day, 43) < VentureCatalogue.AcquireChance * Math.Min(3, s.Stage) * mood / 3000)
            {
                long price = s.Mrr * 12 * TechRandom.Range(f.Seed, s.Id, day, 44, VentureCatalogue.AcquireMultipleLeast, VentureCatalogue.AcquireMultipleMost);
                Sell(f, s, Math.Max(price, s.Post / 2), NoteAcquired, notes);
                return;
            }
            if (s.Alive && s.Stage == (int)RoundStage.Growth && s.Mrr * 12 >= VentureCatalogue.ListingArr && TechRandom.Mille(f.Seed, s.Id, day, 45) < 20)
            {
                List(f, s, notes);
            }
        }

        // The round closes with a lead if the bar is met and a firm comes; else perhaps a down round, else a failed raise.
        private static void Close(VentureFile f, Startup s, int day, int mood, int growth, List<VentureNote> notes)
        {
            int next = s.Stage + 1;
            bool bar = s.Mrr >= VentureCatalogue.RoundBar[TalentCatalogue.Clamp(next, 0, (int)RoundStage.Count - 1)] || (next <= (int)RoundStage.Seed && s.Quality >= 600);
            int lead = bar ? Lead(f, s, next, mood, day) : -1;
            long pre = PreMoney(s.Mrr, growth, mood, next);
            long size = RoundSize(next);
            if (lead >= 0)
            {
                Led(s, lead, pre, size, day, notes);
                return;
            }
            s.FailedRaises++;
            if (s.Stage >= (int)RoundStage.Seed && s.FailedRaises == 1 && TechRandom.Mille(f.Seed, s.Id, day, 47) < 500 * mood / 1000)
            {
                // a down round: an insider takes it at a lower value, for less money
                long down = Math.Max(1, s.Post * (1000 - VentureCatalogue.DownRoundCut) / 1000);
                int insider = InsiderVc(s);
                var vc = new Holder { Kind = (int)HolderKind.Vc, Who = insider < 0 ? 5 : insider, Invested = size / 2 };
                int stage = s.Stage;
                Round(s, down, new[] { vc }, day);
                s.Stage = stage;   // a down round raises money but is no new stage
                s.Burn = VentureCatalogue.BurnByStage[stage];
                notes?.Add(new VentureNote { Kind = NoteDown, Startup = s.Id, Amount = size / 2 });
                FollowOn(s, notes);
                return;
            }
            s.State = (int)StartupState.Running;
            s.LastRound = day;
            s.Ask = null;
        }

        /// <summary>A round a venture firm leads for all of it: the round closes, and a player with pro rata rights is offered their follow-on.</summary>
        public static void Led(Startup s, int lead, long pre, long size, int day, List<VentureNote> notes)
        {
            var vc = new Holder { Kind = (int)HolderKind.Vc, Who = lead, Invested = size, ProRata = true, Board = !VentureCatalogue.Firms[lead].Angels };
            Round(s, pre, new[] { vc }, day);
            notes?.Add(new VentureNote { Kind = NoteRaised, Startup = s.Id, Amount = size });
            FollowOn(s, notes);
        }

        /// <summary>A firm that leads rounds at the stage, the first in the list; -1 if none does.</summary>
        public static int AnyLead(int stage)
        {
            for (int i = 0; i < VentureCatalogue.Firms.Length; i++)
            {
                if (stage >= VentureCatalogue.Firms[i].Least && stage <= VentureCatalogue.Firms[i].Most)
                {
                    return i;
                }
            }
            return -1;
        }

        private static int InsiderVc(Startup s)
        {
            for (int i = s.Holders.Count - 1; i >= 0; i--)
            {
                if (s.Holders[i].Kind == (int)HolderKind.Vc)
                {
                    return s.Holders[i].Who;
                }
            }
            return -1;
        }

        // After a round: a player who holds pro rata rights is offered their share of it.
        private static void FollowOn(Startup s, List<VentureNote> notes)
        {
            if (FollowOnAmount(s) > 0)
            {
                notes?.Add(new VentureNote { Kind = NoteFollowOn, Startup = s.Id });
            }
        }

        /// <summary>The shares the player holds in a startup, as an angel and from the fund.</summary>
        public static long PlayerShares(Startup s)
        {
            long mine = 0;
            for (int i = 0; i < s.Holders.Count; i++)
            {
                mine += s.Holders[i].Players ? s.Holders[i].Shares : 0;
            }
            return mine;
        }

        /// <summary>True if one of the player's lines holds pro rata rights.</summary>
        public static bool HasProRata(Startup s)
        {
            for (int i = 0; i < s.Holders.Count; i++)
            {
                if (s.Holders[i].Players && s.Holders[i].ProRata)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>All the money put into a startup, cents.</summary>
        public static long Invested(Startup s)
        {
            long t = 0;
            for (int i = 0; i < s.Holders.Count; i++)
            {
                t += s.Holders[i].Invested;
            }
            return t;
        }

        /// <summary>A startup bought (or wound up for nothing): the waterfall pays everyone; the note says what came back to the player.</summary>
        public static long[] Sell(VentureFile f, Startup s, long price, int note, List<VentureNote> notes)
        {
            long[] pay = Waterfall(s.Holders, price);
            long player = 0;
            for (int i = 0; i < s.Holders.Count; i++)
            {
                s.Holders[i].Paid = pay[i];
                player += s.Holders[i].Players ? pay[i] : 0;
            }
            s.State = price > 0 ? (int)StartupState.Acquired : (int)StartupState.Failed;
            s.Ask = null;
            notes?.Add(new VentureNote { Kind = note, Startup = s.Id, Amount = player });
            return pay;
        }

        /// <summary>A growth company lists at the public multiple of its revenue; every holder sells at the offer.</summary>
        public static long[] List(VentureFile f, Startup s, List<VentureNote> notes)
        {
            long price = Math.Max(s.Post, s.Mrr * 12 * VentureCatalogue.ListingMultiple);
            long total = Math.Max(1, s.Total);
            var pay = new long[s.Holders.Count];
            long player = 0;
            for (int i = 0; i < s.Holders.Count; i++)
            {
                pay[i] = (long)((double)price * s.Holders[i].Shares / total);
                s.Holders[i].Paid = pay[i];
                player += s.Holders[i].Players ? pay[i] : 0;
            }
            s.Post = price;
            s.State = (int)StartupState.Listed;
            s.Ask = null;
            notes?.Add(new VentureNote { Kind = NoteListing, Startup = s.Id, Amount = player });
            return pay;
        }

        /// <summary>The player's share of a startup and what it is worth at its last round, cents.</summary>
        public static long PlayerValue(Startup s, out int shareMille, out long invested)
        {
            long shares = 0;
            invested = 0;
            for (int i = 0; i < s.Holders.Count; i++)
            {
                if (s.Holders[i].Players)
                {
                    shares += s.Holders[i].Shares;
                    invested += s.Holders[i].Invested;
                }
            }
            long total = Math.Max(1, s.Total);
            shareMille = (int)(shares * 1000 / total);
            return s.Alive ? (long)((double)s.Post * shares / total) : 0;
        }

        /// <summary>The player takes up the founders' ask: the round closes now, a firm leading the rest if one comes. True if done.</summary>
        public static bool Invest(VentureFile f, Startup s, int kind, bool board, int day, int cycle, int hype, List<VentureNote> notes)
        {
            if (s.Ask == null || !s.Alive || day > s.Ask.Lapse)
            {
                return false;
            }
            int mood = Mood(cycle);
            int next = s.Stage + 1;
            var investors = new List<Holder>(2) { new Holder { Kind = kind, Invested = s.Ask.Amount, ProRata = true, Board = board } };
            int lead = Lead(f, s, next, mood + 300, day);
            long rest = RoundSize(next) - s.Ask.Amount;
            if (lead >= 0 && rest > 0)
            {
                investors.Add(new Holder { Kind = (int)HolderKind.Vc, Who = lead, Invested = rest, ProRata = true, Board = !VentureCatalogue.Firms[lead].Angels });
            }
            long raised = 0;
            for (int i = 0; i < investors.Count; i++)
            {
                raised += investors[i].Invested;
            }
            Round(s, s.Ask.Pre, investors, day);
            notes?.Add(new VentureNote { Kind = NoteRaised, Startup = s.Id, Amount = raised });
            return true;
        }

        /// <summary>The player asks for a lower value; founders with a weaker hand take the cut. True if the ask came down.</summary>
        public static bool Haggle(VentureFile f, Startup s, int cycle, int day)
        {
            if (s.Ask == null)
            {
                return false;
            }
            int chance = 700 - s.Quality / 2 + s.FailedRaises * 200 - (Mood(cycle) - 1000) / 2;
            bool yes = TechRandom.Mille(f.Seed, s.Id, day, 51) < chance;
            if (yes)
            {
                s.Ask.Pre = s.Ask.Pre * (1000 - VentureCatalogue.HaggleCut) / 1000;
            }
            s.Ask.Lapse = Math.Min(s.Ask.Lapse, day + 3);
            return yes;
        }

        /// <summary>
        /// A follow-on, cents asked: the player's part of the last round, at its price, that brings them back to the share they held
        /// just before it, never more. 0 once taken, with no pro rata rights, for a round the player held nothing before, for a round
        /// that left them at or above that share (a round they joined), and for a round closed before the save kept the share.
        /// </summary>
        public static long FollowOnAmount(Startup s)
        {
            long others = s.BeforeTotal - s.BeforeMine;
            if (s.Followed || s.BeforeMine <= 0 || others <= 0 || !HasProRata(s))
            {
                return 0;
            }
            long total = Math.Max(1, s.Total);
            // the new shares x that make (mine + x) / (total + x) the share before: x = (before mine * total - mine * before total) / others
            double shares = ((double)s.BeforeMine * total - (double)PlayerShares(s) * s.BeforeTotal) / others;
            if (shares < 1)
            {
                return 0;
            }
            return (long)(shares * Math.Max(1, s.Post) / total);
        }

        /// <summary>True while the player may take the follow-on of the last round: within its days, not yet taken, and something to take.</summary>
        public static bool FollowOnOpen(Startup s, int day)
        {
            return s.Alive && s.RoundDay >= 0 && day >= s.RoundDay && day - s.RoundDay <= VentureCatalogue.ProRataDays && FollowOnAmount(s) > 0;
        }

        /// <summary>
        /// The player's follow-on: new shares at the last round's price per share (the value after it over its shares), the money
        /// added to the value as the round grew by it. Once a round; refused above <see cref="FollowOnAmount"/>. True if taken.
        /// </summary>
        public static bool TakeFollowOn(Startup s, int kind, long amount)
        {
            if (amount <= 0 || amount > FollowOnAmount(s))
            {
                return false;
            }
            long total = Math.Max(1, s.Total);
            long price = Math.Max(1, s.Post);
            var h = new Holder { Kind = kind, Invested = amount, Round = s.Stage, ProRata = true };
            h.Shares = (long)((double)amount * total / price);
            h.Preference = amount * VentureCatalogue.LiquidationPreference / 1000;
            s.Holders.Add(h);
            s.Cash += amount;
            s.Post += amount;
            s.Followed = true;
            return true;
        }

        /// <summary>What a startup's deck says its revenue is: the truth, stretched by its claim.</summary>
        public static long Claimed(Startup s) => s.Mrr * (10000 + s.ClaimBp) / 10000;
    }
}
