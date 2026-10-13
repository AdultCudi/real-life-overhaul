using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Tech
{
    /// <summary>
    /// The rules of startups and venture capital: the market's mood and the AI wave, a startup's birth and day (growth,
    /// burn, the runway, raising a round, a down round, failing, being bought), rounds and the dilution they bring, a
    /// sale's waterfall with its liquidation preferences, what a startup asks of the player, and the player's own firms
    /// raising (term sheets, the board and its targets, bridges, share options and a sale of shares). Game-free and
    /// checked by EconomyCheck; every roll is <see cref="TechRandom"/>'s.
    /// </summary>
    public static class VentureRules
    {
        public const int NoteFounded = 1, NoteRaised = 2, NoteDown = 3, NoteFailed = 4, NoteAcquired = 5, NotePitch = 6, NoteFollowOn = 7, NoteListing = 8, NoteAcquihire = 9;
        public const long FounderShares = 10000000;

        // ---------------------------------------------------------------- the mood

        /// <summary>The market's mood for startups, thousandths: the city's cycle.</summary>
        public static int Mood(int cycle) => VentureCatalogue.MoodByCycle[TalentCatalogue.Clamp(cycle, 0, 3)];

        /// <summary>The AI wave, thousandths: rising and falling over a couple of the game's years, as hype does.</summary>
        public static int Hype(int seed, int day, int year)
        {
            double period = Math.Max(60, year) * VentureCatalogue.HypeYears;
            double phase = (TechRandom.Mille(seed, 1, 2) / 1000.0) * 2 * Math.PI;
            return 1000 + (int)(VentureCatalogue.HypeSwing * Math.Sin(2 * Math.PI * day / period + phase));
        }

        // ---------------------------------------------------------------- a startup's life

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
            s.Name = VentureCatalogue.NameA[TechRandom.Range(f.Seed, id, 7, 0, 0, VentureCatalogue.NameA.Length - 1)] + VentureCatalogue.NameB[TechRandom.Range(f.Seed, id, 8, 0, 0, VentureCatalogue.NameB.Length - 1)];
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

        /// <summary>A round's value before the money, cents: a multiple of its recurring revenue a year (higher for faster growth and a better mood), never under the stage's floor.</summary>
        public static long PreMoney(long mrr, int growth, int mood, int stage)
        {
            int span = VentureCatalogue.GrowthBest - VentureCatalogue.GrowthWorst;
            long multiple = VentureCatalogue.MultipleLeast * 1000L + (long)(VentureCatalogue.MultipleMost - VentureCatalogue.MultipleLeast) * 1000 * TalentCatalogue.Clamp(growth - VentureCatalogue.GrowthWorst, 0, span) / span;
            multiple = multiple * mood / 1000;
            long byRevenue = mrr * 12 * multiple / 1000;
            return Math.Max(VentureCatalogue.FloorValue[TalentCatalogue.Clamp(stage, 0, (int)RoundStage.Count - 1)], byRevenue);
        }

        /// <summary>The size of the next round: eighteen months of the next stage's burn.</summary>
        public static long RoundSize(int nextStage)
        {
            return (long)VentureCatalogue.BurnByStage[TalentCatalogue.Clamp(nextStage, 0, (int)RoundStage.Count - 1)] * 30 * VentureCatalogue.RunwayMonthsRaised;
        }

        /// <summary>The lead for a round: a firm that leads at the stage, keen on the sector if one is; -1 for none (the mood decides whether anyone comes).</summary>
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
        /// A round closes: new shares at the price the value before the money gives; each investor's line added (a lead
        /// with a board seat and pro rata rights, every investor a one-times preference); at the seed round a pool for staff
        /// is set aside out of the founders' share. The stage moves on, the money is in, the burn grows.
        /// </summary>
        public static void Round(Startup s, long pre, IList<Holder> investors, int day)
        {
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

        /// <summary>
        /// A sale's money shared out by the cap table: an investor whose share of the price is less than their preference
        /// takes the preference instead (the latest round first while the money lasts); everyone else shares what is left
        /// by their shares. Returns each line's money, in the cap table's order.
        /// </summary>
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
            // who takes the preference: worked out again until nobody changes their mind
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
            // preferences, latest round first
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
            // the rest by shares
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
                    pay[last] += left - given;   // (what rounding left over)
                }
            }
            return pay;
        }

        /// <summary>
        /// A day of one of the city's startups. Revenue grows by its month's growth; money goes by its burn less its
        /// revenue. With under six months of runway it raises: on the first day it may come to the player as well (an
        /// ask); after a week and a half the round closes with a lead if its revenue meets the stage's bar and a firm comes
        /// (the player's own money in it, if they took the ask), a down round if it does not, and failure (or an acqui-hire)
        /// when the money runs out after two failed raises. From the seed round a buyer may come (more often the later the
        /// round); a growth company big enough
        /// could list.
        /// </summary>
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
                    s.Ask = new TermSheet { Amount = Math.Max(VentureCatalogue.TicketLeast, round * VentureCatalogue.AskPlayerShare / 1000), Pre = PreMoney(s.Mrr, growth, mood, s.Stage + 1), Lapse = day + 7, ProRata = true };
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
                s.Cash = 0;   // (the founders go without pay until the raise)
            }
            if (s.Alive && s.Stage >= (int)RoundStage.Seed && TechRandom.Mille(f.Seed, s.Id, day, 43) < VentureCatalogue.AcquireChance * Math.Min(3, s.Stage) * mood / 3000)
            {
                long price = s.Mrr * 12 * TechRandom.Range(f.Seed, s.Id, day, 44, VentureCatalogue.AcquireMultipleLeast, VentureCatalogue.AcquireMultipleMost);
                Sell(f, s, Math.Max(price, s.Post / 2), NoteAcquired, notes);
                return;
            }
            if (s.Alive && s.Stage == (int)RoundStage.Growth && s.Mrr * 12 >= VentureCatalogue.ListingArr && TechRandom.Mille(f.Seed, s.Id, day, 45) < 20)
            {
                notes?.Add(new VentureNote { Kind = NoteListing, Startup = s.Id });
            }
        }

        // The round closes: a lead if the bar is met and a firm comes; a down round if the bar is met by nobody; or a failed raise.
        private static void Close(VentureFile f, Startup s, int day, int mood, int growth, List<VentureNote> notes)
        {
            int next = s.Stage + 1;
            bool bar = s.Mrr >= VentureCatalogue.RoundBar[TalentCatalogue.Clamp(next, 0, (int)RoundStage.Count - 1)] || (next <= (int)RoundStage.Seed && s.Quality >= 600);
            int lead = bar ? Lead(f, s, next, mood, day) : -1;
            long pre = PreMoney(s.Mrr, growth, mood, next);
            long size = RoundSize(next);
            if (lead >= 0)
            {
                var vc = new Holder { Kind = (int)HolderKind.Vc, Who = lead, Invested = size, ProRata = true, Board = !VentureCatalogue.Firms[lead].Angels };
                Round(s, pre, new[] { vc }, day);
                notes?.Add(new VentureNote { Kind = NoteRaised, Startup = s.Id, Amount = size });
                FollowOn(s, notes);
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
                s.Stage = stage;   // (a down round raises money but is no new stage)
                s.Burn = VentureCatalogue.BurnByStage[stage];
                notes?.Add(new VentureNote { Kind = NoteDown, Startup = s.Id, Amount = size / 2 });
                return;
            }
            s.State = (int)StartupState.Running;
            s.LastRound = day;
            s.Ask = null;
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
            for (int i = 0; i < s.Holders.Count; i++)
            {
                if (s.Holders[i].Players && s.Holders[i].ProRata)
                {
                    notes?.Add(new VentureNote { Kind = NoteFollowOn, Startup = s.Id });
                    return;
                }
            }
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

        // ---------------------------------------------------------------- the player as an investor

        /// <summary>The player takes up the founders' ask: the round closes now with the player's money in it, a firm leading the rest if one comes. True if done.</summary>
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

        /// <summary>
        /// The player asks for a lower value: founders with a weaker hand (lower quality, a failed raise, a poor mood) take
        /// the cut; the others hold to their ask. True if the ask came down.
        /// </summary>
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

        /// <summary>A follow-on: the player keeps their share of the last round by putting in their part of it. Cents asked.</summary>
        public static long FollowOnAmount(Startup s)
        {
            long total = Math.Max(1, s.Total), mine = 0, last = 0;
            for (int i = 0; i < s.Holders.Count; i++)
            {
                if (s.Holders[i].Players)
                {
                    mine += s.Holders[i].Shares;
                }
                if (s.Holders[i].Round == s.Stage && !s.Holders[i].Players)
                {
                    last += s.Holders[i].Invested;
                }
            }
            return (long)((double)last * mine / total);
        }

        /// <summary>The player's follow-on: new shares at the last round's price, which dilutes the others a little.</summary>
        public static void TakeFollowOn(Startup s, int kind, long amount)
        {
            long total = Math.Max(1, s.Total);
            long price = Math.Max(1, s.Post);
            var h = new Holder { Kind = kind, Invested = amount, Round = s.Stage, ProRata = true };
            h.Shares = (long)((double)amount * total / price);
            h.Preference = amount * VentureCatalogue.LiquidationPreference / 1000;
            s.Holders.Add(h);
            s.Cash += amount;
            s.Post += amount;
        }

        /// <summary>What a startup's deck says its revenue is: the truth, stretched by its claim.</summary>
        public static long Claimed(Startup s) => s.Mrr * (10000 + s.ClaimBp) / 10000;

        // ---------------------------------------------------------------- the player's own firms

        /// <summary>A cap table for a firm of the player's that is raising for the first time: the player holds it all.</summary>
        public static Cap NewCap(string place)
        {
            var c = new Cap { Place = place, Stage = -1 };
            c.Holders.Add(new Holder { Kind = (int)HolderKind.Player, Shares = FounderShares });
            return c;
        }

        /// <summary>The player's share of a firm, thousandths.</summary>
        public static int PlayerShare(Cap c)
        {
            long total = Math.Max(1, c.Total), mine = 0;
            for (int i = 0; i < c.Holders.Count; i++)
            {
                mine += c.Holders[i].Kind == (int)HolderKind.Player ? c.Holders[i].Shares : 0;
            }
            return (int)(mine * 1000 / total);
        }

        /// <summary>
        /// A firm's term sheet from a firm met: the value before the money is what the firm is worth by its revenue and
        /// growth, moved by how the meeting went (six tenths to one and a half) and the mood; the amount is what was asked
        /// for; a lead takes a board seat and pro rata rights.
        /// </summary>
        public static TermSheet Sheet(int vc, long firmValue, long amount, int score, int mood, int day)
        {
            long pre = firmValue * (600 + TalentCatalogue.Clamp(score, 0, 1000) * 9 / 10) / 1000 * mood / 1000;
            pre = Math.Max(VentureCatalogue.FloorValue[0], pre);
            return new TermSheet { Vc = vc, Amount = amount, Pre = pre, Board = !VentureCatalogue.Firms[TalentCatalogue.Clamp(vc, 0, VentureCatalogue.FirmCount - 1)].Angels, ProRata = true, Lapse = day + VentureCatalogue.TermSheetDays, Score = score };
        }

        /// <summary>
        /// A term sheet taken: the firm's new shares go to the investor (a bridge owed converts at a discount to its price
        /// too), the board gains the investor's seat, and the board sets a growth target for the quarter. Returns the money
        /// paid into the firm, cents.
        /// </summary>
        public static long Accept(Cap c, TermSheet t, long mrr, int day)
        {
            long total = Math.Max(1, c.Total);
            var h = new Holder { Kind = (int)HolderKind.Vc, Who = t.Vc, Invested = t.Amount, Board = t.Board, ProRata = t.ProRata, Round = c.Stage + 1 };
            h.Shares = (long)((double)t.Amount * total / Math.Max(1, t.Pre));
            h.Preference = t.Amount * VentureCatalogue.LiquidationPreference / 1000;
            c.Holders.Add(h);
            if (c.Bridge > 0)
            {
                long price = Math.Max(1, t.Pre) * (1000 - VentureCatalogue.BridgeDiscount) / 1000;
                c.Holders.Add(new Holder { Kind = (int)HolderKind.Vc, Who = t.Vc, Invested = c.Bridge, Round = c.Stage + 1, Shares = (long)((double)c.Bridge * total / price), Preference = c.Bridge });
                c.Bridge = 0;
            }
            c.Stage = Math.Min((int)RoundStage.Growth, c.Stage + 1);
            c.Post = t.Pre + t.Amount;
            if (t.Board)
            {
                c.BoardSeats += VentureCatalogue.BoardSeatsPerRound;
                c.VcSeats += VentureCatalogue.BoardSeatsPerRound;
            }
            c.RaiseUntil = 0;
            c.Sheets.Clear();
            c.Misses = 0;   // (a new round: the board starts afresh)
            c.Pressure = 0;
            Target(c, mrr, day);
            return t.Amount;
        }

        /// <summary>The board's target: recurring revenue a quarter on, a set share higher than now.</summary>
        public static void Target(Cap c, long mrr, int day)
        {
            c.LastMrr = mrr;
            c.TargetMrr = Math.Max(100000, mrr * (1000 + VentureCatalogue.TargetGrowth) / 1000);
            c.TargetDay = day + VentureCatalogue.TargetDays;
        }

        /// <summary>
        /// The board looks at its target on its day: met, a new one is set and misses are forgotten; missed, the board
        /// warns; missed twice with a majority of the board the investors', it replaces the chief executive. Returns 0 if
        /// nothing was due, 1 met, 2 warned, 3 replaced.
        /// </summary>
        public static int Review(Cap c, long mrr, int day)
        {
            if (c.VcSeats == 0 || c.TargetDay <= 0 || day < c.TargetDay)
            {
                return 0;
            }
            if (mrr >= c.TargetMrr)
            {
                c.Misses = 0;
                c.Pressure = 0;
                Target(c, mrr, day);
                return 1;
            }
            c.Misses++;
            Target(c, mrr, day);
            if (c.Misses >= VentureCatalogue.MissesToReplace && c.VcSeats * 2 > c.BoardSeats)
            {
                c.Pressure = 2;
                c.Misses = 0;
                return 3;
            }
            c.Pressure = 1;
            return 2;
        }

        /// <summary>A bridge loan from the last lead: six months of a burn, owed with interest until the next round converts it.</summary>
        public static long BridgeFor(long burnPerDay) => Math.Max(0, burnPerDay) * 30 * VentureCatalogue.BridgeMonths;

        /// <summary>A day's interest on a bridge, cents.</summary>
        public static long BridgeInterest(long owed) => owed * VentureCatalogue.BridgeInterestBp / 10000 / 365;

        /// <summary>Share options granted: new shares for the staff pool. Returns the shares made.</summary>
        public static long Grant(Cap c, int thousandths)
        {
            long total = Math.Max(1, c.Total);
            long shares = total * thousandths / (1000 - thousandths);
            Holder pool = null;
            for (int i = 0; i < c.Holders.Count && pool == null; i++)
            {
                pool = c.Holders[i].Kind == (int)HolderKind.Pool ? c.Holders[i] : null;
            }
            if (pool == null)
            {
                pool = new Holder { Kind = (int)HolderKind.Pool };
                c.Holders.Add(pool);
            }
            pool.Shares += shares;
            c.OptionsGranted += thousandths;
            return shares;
        }

        /// <summary>The share of a grant that has vested: none in the first year, then by the day over four years.</summary>
        public static int Vested(int grantedDay, int day, int year)
        {
            int y = Math.Max(60, year);
            int age = day - grantedDay;
            if (age < VentureCatalogue.CliffYears * y)
            {
                return 0;
            }
            return Math.Min(1000, age * 1000 / (VentureCatalogue.VestYears * y));
        }

        /// <summary>The player sells some of their shares to an investor at a fifth off the last round's price. Returns the cents paid to the player.</summary>
        public static long SellShares(Cap c, int thousandthsOfMine, int vc)
        {
            Holder me = null;
            for (int i = 0; i < c.Holders.Count && me == null; i++)
            {
                me = c.Holders[i].Kind == (int)HolderKind.Player ? c.Holders[i] : null;
            }
            if (me == null || c.Post <= 0)
            {
                return 0;
            }
            long total = Math.Max(1, c.Total);
            long shares = me.Shares * TalentCatalogue.Clamp(thousandthsOfMine, 0, 1000) / 1000;
            long price = (long)((double)c.Post * shares / total * 800 / 1000);
            me.Shares -= shares;
            c.Holders.Add(new Holder { Kind = (int)HolderKind.Vc, Who = vc, Shares = shares, Invested = price, Round = Math.Max(0, c.Stage) });
            return price;
        }
    }
}
