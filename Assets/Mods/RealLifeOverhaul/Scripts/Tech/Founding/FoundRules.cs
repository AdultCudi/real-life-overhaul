using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Tech
{
    /// <summary>What the product shows the fit rule on a day: read from the sector's rules, never invented. Game-free.</summary>
    public struct FitInputs
    {
        public int Appeal;           // thousandths, 1000 the market's going appeal
        public int Soundness;        // thousandths: how few bugs
        public int Reputation;       // thousandths: the firm's name
        public long Users;           // paying customers
        public long NicheBuyers;     // the niche's buyers now
        public int ChurnBase;        // thousandths a month a sound product of the segment loses (FirmsCatalogue.Churn)
    }

    /// <summary>
    /// The rules of founding a startup (the plan's section 7): the split of shares and the vesting of a co-founder's, the pre-seed sources and where each lands, the articles and
    /// what they let the board do, and going to market: discovery, the first version, early users and fit, the launch channel and the price. Whole numbers only. Game-free:
    /// EconomyCheck's StartupFoundingChecks hold it.
    /// </summary>
    public static class FoundRules
    {
        // ---------------------------------------------------------------- incorporation and the split

        /// <summary>
        /// A cap table for a new startup: the founders' shares between the player and the co-founders who join (<paramref name="splitMille"/> of them go to the co-founders, shared
        /// evenly), the shares of each vesting from the day. Returns the cap; the co-founders' lines carry their number in Holder.Who.
        /// </summary>
        public static Cap Incorporate(string place, IList<CoFounder> co, int splitMille, int day)
        {
            Cap c = VentureRules.NewCap(place);
            int n = co != null ? co.Count : 0;
            if (n == 0)
            {
                return c;
            }
            long theirs = VentureRules.FounderShares * Math.Max(0, Math.Min(FoundCatalogue.SplitMost, splitMille)) / 1000;
            long each = theirs / n;
            Holder me = c.Holders[0];
            me.Shares = VentureRules.FounderShares - each * n;
            for (int i = 0; i < n; i++)
            {
                co[i].Day = day;
                co[i].Shares = each;
                c.Holders.Add(new Holder { Kind = (int)HolderKind.Founders, Who = co[i].No, Shares = each, Round = 0 });
            }
            return c;
        }

        /// <summary>The line of a co-founder on the cap table, or null.</summary>
        public static Holder LineOf(Cap c, int no)
        {
            for (int i = 0; c != null && i < c.Holders.Count; i++)
            {
                if (c.Holders[i].Kind == (int)HolderKind.Founders && c.Holders[i].Who == no)
                {
                    return c.Holders[i];
                }
            }
            return null;
        }

        /// <summary>The thousandths of a co-founder's shares vested today: nothing before the cliff, then by the day over four years.</summary>
        public static int Vested(CoFounder co, int day, int year) => VentureRules.Vested(co.Day, day, year);

        /// <summary>
        /// A co-founder leaves: before the one year cliff they take nothing, after it the part that has vested. What does not vest is cancelled, so everybody else's share grows.
        /// Returns the shares cancelled.
        /// </summary>
        public static long Leave(Cap c, CoFounder co, int day, int year)
        {
            if (co.Left > 0)
            {
                return 0L;
            }
            Holder line = LineOf(c, co.No);
            co.Left = day;
            if (line == null)
            {
                co.Kept = 0;
                return 0L;
            }
            long keep = line.Shares * Vested(co, day, year) / 1000;
            long cancelled = line.Shares - keep;
            line.Shares = keep;
            co.Kept = keep;
            if (keep <= 0)
            {
                c.Holders.Remove(line);
            }
            return cancelled;
        }

        // ---------------------------------------------------------------- fees and articles

        /// <summary>The incorporation fee, cents at the start's prices: a few days of a lawyer, more for articles that hold the board back, less for a player who knows the law.</summary>
        public static long Fee(int articles, int law)
        {
            long fee = (long)VentureCatalogue.DiligenceFee * FoundCatalogue.FeeTickets;
            if (articles == FoundCatalogue.ArticlesFriendly)
            {
                fee = fee * (1000 + FoundCatalogue.FriendlyFeeExtra) / 1000;
            }
            return fee * (1000 - FoundCatalogue.LawDiscount * Math.Max(0, Math.Min(100, law))) / 1000;
        }

        /// <summary>
        /// Whether the board can remove the founder as chief executive (the consequence ladder's third step): the investors must hold a seat at all, and under founder friendly
        /// articles hold the board's majority. A firm with no record of founding keeps the ladder as it was.
        /// </summary>
        public static bool CanRemove(FoundedFirm f, Cap c)
        {
            if (f == null || !f.Counts)
            {
                return true;
            }
            if (c.VcSeats <= 0)
            {
                return false;
            }
            return f.Articles != FoundCatalogue.ArticlesFriendly || c.VcSeats * 2 > c.BoardSeats;
        }

        /// <summary>Thousandths of the value an investor puts on the firm, which articles take off.</summary>
        public static int ArticlesFactor(FoundedFirm f) => f != null && f.Counts && f.Articles == FoundCatalogue.ArticlesFriendly ? 1000 - FoundCatalogue.FriendlyPriceCut : 1000;

        // ---------------------------------------------------------------- the pre-seed sources

        /// <summary>What friends and family put in at a step of their offer, cents.</summary>
        public static long FamilyAmount(int step) => VentureCatalogue.TicketLeast * FoundCatalogue.FamilyTickets[Math.Max(0, Math.Min(FoundCatalogue.FamilyTickets.Length - 1, step))];

        public static long AccelAmount() => VentureCatalogue.TicketLeast * FoundCatalogue.AccelTickets;

        /// <summary>The value friends and family put on a firm before their money, cents.</summary>
        public static long FamilyPre() => VentureCatalogue.FloorValue[0] * FoundCatalogue.FamilyValue / 1000;

        /// <summary>Where a pre-seed line lands on a cap table: the stage is pre-seed, the value after is what it came in at.</summary>
        private static void Land(Cap c, long pre, long amount)
        {
            c.Stage = Math.Max(c.Stage, (int)RoundStage.PreSeed);
            c.Post = Math.Max(c.Post, pre + amount);
        }

        /// <summary>
        /// Friends and family: shares at their own value of the firm, no seat and no preference. The money is the investors' from the account's first day, so it counts for the
        /// floor. Returns the cents put in.
        /// </summary>
        public static long TakeFamily(Cap c, long amount, int day)
        {
            long pre = FamilyPre();
            long total = Math.Max(1, c.Total);
            c.Holders.Add(new Holder
            {
                Kind = (int)HolderKind.Vc, Who = VentureCatalogue.FamilyId, Invested = amount, Round = 0, Shares = (long)((double)amount * total / Math.Max(1, pre)), Preference = amount
            });
            Land(c, pre, amount);
            StartupMoney.Inflow(c, amount, day);
            return amount;
        }

        /// <summary>The accelerator: a small sum for a set share, no seat. Returns the cents put in.</summary>
        public static long TakeAccelerator(Cap c, int day)
        {
            long amount = AccelAmount();
            long total = Math.Max(1, c.Total);
            int share = FoundCatalogue.AccelShare;
            c.Holders.Add(new Holder
            {
                Kind = (int)HolderKind.Vc, Who = VentureCatalogue.AcceleratorId, Invested = amount, Round = 0, Shares = total * share / (1000 - share), Preference = amount
            });
            Land(c, amount * 1000 / share - amount, amount);
            StartupMoney.Inflow(c, amount, day);
            return amount;
        }

        /// <summary>An angel's cheque without a pitch: the term sheet the angel offers a founder by what they bring, priced the way every sheet is, then by the articles.</summary>
        public static TermSheet AngelSheet(int vc, long firmValue, int selling, int relationships, int mood, int day, FoundedFirm f)
        {
            int score = FoundCatalogue.AngelScoreBase + FoundCatalogue.AngelScorePoint * (Math.Max(0, Math.Min(100, selling)) + Math.Max(0, Math.Min(100, relationships)));
            long amount = VentureCatalogue.TicketLeast * FoundCatalogue.AngelTickets;
            amount = Math.Max(amount, firmValue / 4);
            TermSheet t = VentureRules.Sheet(vc, firmValue, amount, Math.Min(1000, score), mood, day);
            t.Pre = Math.Max(1, t.Pre * ArticlesFactor(f) / 1000);
            return t;
        }

        /// <summary>The sheets a demo day brings: venture firms that lead the seed round and are not shut, each pricing the firm by how much it has learned and how far it has come.</summary>
        public static TermSheet DemoSheet(int vc, long firmValue, FoundedFirm f, int mood, int day)
        {
            int score = Math.Min(1000, 300 + f.Learned * 4 / 10 + f.Fit * 3 / 10);
            long amount = Math.Max(VentureCatalogue.TicketLeast * 4, firmValue / 4);
            TermSheet t = VentureRules.Sheet(vc, firmValue, amount, score, mood, day);
            t.Pre = Math.Max(1, t.Pre * ArticlesFactor(f) / 1000);
            return t;
        }

        /// <summary>The share of a family's money the firm lost when it closed, thousandths, from what the wind down paid the family's line against what it put in.</summary>
        public static int FamilyLoss(long paid, long invested) => invested <= 0 ? 0 : (int)Math.Max(0L, Math.Min(1000L, (invested - Math.Max(0L, paid)) * 1000 / invested));

        // ---------------------------------------------------------------- going to market

        /// <summary>A fresh record for a firm just founded.</summary>
        public static FoundedFirm Found(string place, int day, int sector, int niche, int articles, int source, long raised, long fee)
        {
            return new FoundedFirm
            {
                Place = place, Day = day, Sector = sector, Niche = niche, Articles = articles, Source = source, Raised = raised, Fee = fee, Stage = FoundCatalogue.StageDiscovery
            };
        }

        /// <summary>
        /// What a firm of an older save already is, as a stage: with customers and revenue it is past early users (the launch is next), with a product on sale and none yet it
        /// is learning from its first users, with a product being made it is building, otherwise discovering. Nothing it has changes.
        /// </summary>
        public static int PlaceStage(bool hasLive, bool hasBuilding, long users, long takings30)
        {
            if (hasLive && users > 0 && takings30 > 0)
            {
                return FoundCatalogue.StageLaunch;
            }
            if (hasLive)
            {
                return FoundCatalogue.StageUsers;
            }
            return hasBuilding ? FoundCatalogue.StageBuild : FoundCatalogue.StageDiscovery;
        }

        /// <summary>A firm of an older save, placed: at the stage it is, its niche, nothing else changed. The fit it already shows is worked out from the first day.</summary>
        public static FoundedFirm Place(string place, int day, int sector, int niche, int stage, int pmfDay)
        {
            return new FoundedFirm
            {
                Place = place, Day = day, Placed = true, Sector = sector, Niche = niche, Stage = stage, PmfDay = pmfDay, Articles = FoundCatalogue.ArticlesStandard, Learned = 500
            };
        }

        /// <summary>
        /// What a round of customer interviews teaches, thousandths: the way chosen leans on Selling (in person), Relationships (the phone round) or Analysis (a survey), and
        /// Product sense.
        /// </summary>
        public static int Interview(int how, int selling, int relationships, int analysis, int productSense)
        {
            int lean = how == 0 ? selling : how == 1 ? relationships : analysis;
            return FoundCatalogue.LearnBase + FoundCatalogue.LearnPoint * Clamp(lean) + FoundCatalogue.LearnSecond * Clamp(productSense);
        }

        /// <summary>A round of interviews held: what was learned is kept, and the stage moves on after enough rounds. Returns true when it moved on.</summary>
        public static bool Interviewed(FoundedFirm f, int gain)
        {
            f.Learned = Math.Min(1000, f.Learned + Math.Max(0, gain));
            f.Rounds++;
            if (f.Stage == FoundCatalogue.StageDiscovery && f.Rounds >= FoundCatalogue.DiscoveryRounds)
            {
                Next(f, FoundCatalogue.StageBuild);
                return true;
            }
            return false;
        }

        public static void Next(FoundedFirm f, int stage)
        {
            if (stage > f.Stage)
            {
                f.Stage = stage;
                f.Rounds = 0;
            }
        }

        /// <summary>Bugs a round of fixing what makes users leave clears, by Product sense and Analysis.</summary>
        public static int Fixed(int bugs, int productSense, int analysis)
        {
            return Math.Min(Math.Max(0, bugs), Math.Max(1, Math.Max(0, bugs) * (FoundCatalogue.FixBase + FoundCatalogue.FixPoint * (Clamp(productSense) + Clamp(analysis)) / 2) / 1000));
        }

        /// <summary>Word of mouth a session of asking users to spread it adds, thousandths.</summary>
        public static int Word(int relationships) => FoundCatalogue.WomBase + FoundCatalogue.WomPoint * Clamp(relationships);

        /// <summary>The founders' own selling, points a day, for a sales led launch: a seller's points at the founder's Selling.</summary>
        public static int FounderPoints(int selling, int relationships) => FoundCatalogue.FounderSell * (Clamp(selling) * 2 + Clamp(relationships)) / 300;

        /// <summary>Awareness a launch channel adds, thousandths.</summary>
        public static int Boost(int channel) => channel == FoundCatalogue.ChannelProduct ? FoundCatalogue.ProductBoost : channel == FoundCatalogue.ChannelPartner ? FoundCatalogue.PartnerBoost : 0;

        /// <summary>The share of the takings a reselling partner keeps, thousandths.</summary>
        public static int Cut(int channel) => channel == FoundCatalogue.ChannelPartner ? FoundCatalogue.PartnerCut : 0;

        /// <summary>A day's customers won and lost noted: they ease into a month's, so retention is read from a month and not a day.</summary>
        public static void Note(FoundedFirm f, long won, long lost, long users)
        {
            f.WonMonth += (int)((Math.Min(1000000L, won) * 1000 - f.WonMonth) / 30);
            f.LostMonth += (int)((Math.Min(1000000L, lost) * 1000 - f.LostMonth) / 30);
            f.Users = (int)Math.Min(int.MaxValue, Math.Max(0L, users));
        }

        /// <summary>Customers kept, thousandths: all with none lost, three quarters at a sound product's own churn, half at twice it.</summary>
        public static int Retention(FoundedFirm f, int churnBase)
        {
            long users = Math.Max(1, f.Users);
            long lostMonth = (long)f.LostMonth * 30 / 1000;
            long churn = lostMonth * 1000 / users;
            return (int)Math.Max(0L, Math.Min(1000L, 1000 - churn * 250 / Math.Max(1, churnBase)));
        }

        /// <summary>What fit is heading for, thousandths: what the interviews learned, how many customers stay, how many tell others, how good the product is and the firm's name.</summary>
        public static int Target(FoundedFirm f, in FitInputs x)
        {
            long share = x.NicheBuyers > 0 ? Math.Min(1000L, x.Users * 1000 / x.NicheBuyers) : 0;
            int word = (int)Math.Min(1000L, share * FoundCatalogue.WordShare) + Math.Max(0, f.Wom);
            int quality = (Math.Min(1000, x.Appeal * 7 / 10) + Clamp(x.Soundness, 1000)) / 2;
            long t = (long)f.Learned * FoundCatalogue.FitLearned + (long)Retention(f, x.ChurnBase) * FoundCatalogue.FitRetention + (long)Math.Min(1000, word) * FoundCatalogue.FitWord
                     + (long)quality * FoundCatalogue.FitQuality + (long)Clamp(x.Reputation, 1000) * FoundCatalogue.FitName;
            return (int)Math.Max(0L, Math.Min(1000L, t / 1000));
        }

        /// <summary>
        /// A day of a firm going to market. With no product on sale fit is only what the interviews gave it (a quarter of it); with one, fit moves a step towards its target. Word of
        /// mouth eases off. At the bar (with enough customers) the product has found its market. Returns true on the day it does.
        /// </summary>
        public static bool Day(FoundedFirm f, bool live, in FitInputs x, int day)
        {
            f.Wom = Math.Max(0, f.Wom - f.Wom * FoundCatalogue.WomEase / 1000 - (f.Wom > 0 ? 1 : 0));
            if (!live)
            {
                f.Fit = f.Learned / 4;
                return false;
            }
            int target = Target(f, x);
            int step = (target - f.Fit) * FoundCatalogue.FitPace / 1000;
            f.Fit = Math.Max(0, Math.Min(1000, f.Fit + (step == 0 ? Math.Sign(target - f.Fit) : step)));
            bool found = f.PmfDay == 0 && f.Fit >= FoundCatalogue.PmfBar && x.Users >= FoundCatalogue.PmfUsers;
            if (found)
            {
                f.PmfDay = day;
            }
            if (f.Stage == FoundCatalogue.StageBuild)
            {
                Next(f, FoundCatalogue.StageUsers);
            }
            if (f.Stage == FoundCatalogue.StageUsers && f.PmfDay > 0)
            {
                Next(f, FoundCatalogue.StageLaunch);
            }
            return found;
        }

        /// <summary>The launch channel picked: the stage moves to pricing.</summary>
        public static void Launched(FoundedFirm f, int channel, int partner)
        {
            f.Channel = channel;
            f.Partner = channel == FoundCatalogue.ChannelPartner ? partner : -1;
            Next(f, FoundCatalogue.StagePricing);
        }

        /// <summary>The price tested: the stage moves on, the firm is on the market.</summary>
        public static void Priced(FoundedFirm f, int tier)
        {
            f.Price = tier;
            Next(f, FoundCatalogue.StageScaling);
        }

        // ---------------------------------------------------------------- prices and what investors see

        /// <summary>What customers in the niche will bear against its going price, thousandths: fit makes them readier to pay.</summary>
        public static int Bear(FoundedFirm f) => 1000 - FoundCatalogue.PriceBear / 2 + f.Fit * FoundCatalogue.PriceBear / 1000;

        /// <summary>
        /// The thousandths of the value investors put on the firm that fit decides: cheap money before fit (a raise then values the firm at under two thirds), the full value at
        /// the bar, and up to 30 per cent more with a product that has found its market. 1000 for a firm without a founding that counts.
        /// </summary>
        public static int FitFactor(FoundedFirm f)
        {
            if (f == null || !f.Counts)
            {
                return 1000;
            }
            int fit = Math.Max(0, Math.Min(1000, f.Fit));
            if (fit < FoundCatalogue.FitCheap)
            {
                return FoundCatalogue.CheapFactor;
            }
            if (fit < FoundCatalogue.PmfBar)
            {
                return FoundCatalogue.CheapFactor + (1000 - FoundCatalogue.CheapFactor) * (fit - FoundCatalogue.FitCheap) / (FoundCatalogue.PmfBar - FoundCatalogue.FitCheap);
            }
            return 1000 + FoundCatalogue.RichFactor * (fit - FoundCatalogue.PmfBar) / Math.Max(1, 1000 - FoundCatalogue.PmfBar);
        }

        /// <summary>The growth, in basis points, that the valuation's multiple is given for fit (FirmsRules.Value): up with fit over half, down with fit under it.</summary>
        public static int GrowthBp(FoundedFirm f) => f == null || !f.Counts ? 0 : (f.Fit - 500) * FoundCatalogue.FitGrowthBp;

        /// <summary>After fit investors want a seat on the board even for a small cheque; before it they do not ask.</summary>
        public static bool SeatAsked(FoundedFirm f) => f != null && f.Counts && f.PmfDay > 0;

        /// <summary>A niche opened: neighbours only, a few at once, not the one the firm has.</summary>
        public static bool CanExpand(FoundedFirm f, int niche)
        {
            if (f == null || niche < 0 || f.Niche < 0 || f.Expanded.Count + 1 >= NicheCatalogue.Active || niche == f.Niche)
            {
                return false;
            }
            for (int i = 0; i < f.Expanded.Count; i++)
            {
                if (f.Expanded[i].Niche == niche)
                {
                    return false;
                }
            }
            if (NicheRules.Neighbours(f.Niche, niche))
            {
                return true;
            }
            for (int i = 0; i < f.Expanded.Count; i++)
            {
                if (NicheRules.Neighbours(f.Expanded[i].Niche, niche))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>The cost of opening a neighbouring niche, cents: days of what the firm burns (at least a day of a pre-seed firm's).</summary>
        public static long ExpandCost(long burnPerDay) => Math.Max(VentureCatalogue.BurnByStage[0], burnPerDay) * NicheCatalogue.ExpandDays;

        private static int Clamp(int points) => points < 0 ? 0 : points > 100 ? 100 : points;

        private static int Clamp(int v, int most) => v < 0 ? 0 : v > most ? most : v;
    }
}
