using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Tech
{
    /// <summary>One thing the player's wallet paid for that was investors' money: the day, the cents, the transaction's key, and whether the firm was burning cash. Game-free.</summary>
    public sealed class MisuseLine
    {
        public int Day;
        public long Cents;
        public bool Burning;
        public bool Routine;         // the gap came from running costs, rent or a loan's payments, not from a purchase
        public string Key = string.Empty;
        public string Name = string.Empty;   // the business whose running costs it was
    }

    /// <summary>A deal between the firm and another business of the player's: what was paid, and what the mod's own figure for the work is.</summary>
    public sealed class RelatedLine
    {
        public int Day;
        public long Paid, Fair;
        public string Name = string.Empty;
    }

    /// <summary>
    /// A firm's account: a ledger over the player's one wallet for a firm with investors. Raised and bridge in, the firm's own takings
    /// less its costs, less what the player lawfully took out; the part the investors own is what the wallet must keep. Cents at the
    /// start's prices. Game-free.
    /// </summary>
    public sealed class FirmAccount
    {
        public bool Open;
        public bool Wound;           // the firm has been wound down: the account is closed
        public int Opened, GraceTo, FounderDay;
        public long Raised;          // investors' money in: rounds and bridges
        public long Own;             // the firm's own takings less its costs since the account opened, and capital spent at its address
        public long Out;             // lawful withdrawals: salary, dividends, a director's loan outstanding
        public long Taken;           // investors' money spent elsewhere and not paid back
        public int Synced;           // the day Own was last read from the books
        public long Band;            // the founder's salary a month the board allows at this stage
        public int SalaryFrom;
        public long SalaryDrawn;
        public long Loan;            // a director's loan outstanding, principal
        public int LoanDay;
        public bool LoanUsed;
        public long Dividends;       // paid out of profit, all holders together
        public int PackDay;          // the next board pack falls due
        public int PackLast;         // the day of the last one; 0 none yet
        public bool DelayUsed;       // this pack has been delayed once already
        public int DelayDay;         // the day of the last delay; 0 none
        public int Step;             // the consequence ladder, 0 to 5
        public int StepDay, AskDay;
        public bool Stonewalled;
        public int Calm;             // the board will not question again before this day
        public int Event;            // the day of the last thing that made the board uneasy
        public int WarnDay;          // the day the player was last warned of a shortfall
        public int Suit, Case;       // the civil claim and the criminal case, as Justice numbered them; 0 none
        public readonly List<MisuseLine> Misuse = new List<MisuseLine>(4);
        public readonly List<RelatedLine> Related = new List<RelatedLine>(4);
        public int RelatedDeals;
        public long RelatedPaid, RelatedOver;
        public int Gone;             // the first day the firm's business was found gone; 0 while it stands
        public int CapDay;           // the last day the board agreed a deal over the cap on deals with the player's other businesses
        public string Name = string.Empty;   // the firm's name as last seen, for the words once its business is gone
    }

    /// <summary>The figures of the firm account. Cents at the start's prices; a month is 30 days.</summary>
    public static class MoneyCatalogue
    {
        public const int GraceDays = 30;             // an old save's running firm: no floor and no questions for this long
        public const int PackDays = 30;              // between board packs
        public const int PackDelayDays = 14;         // a pack delayed once is this late
        public const int QuestionDays = 7;           // the board's question waits this long for an answer, then counts as stonewalled
        public const int DemandDays = 14;            // a repay demand stands this long
        public const int ClaimWaitDays = 7;          // after the founder is removed, this long before the investors sue
        public const int ClosedDays = 180;           // every venture firm shuts its door this long on a removed founder
        public const int LoanDays = 180, LoanBp = 1000, LoanShare = 100;   // a director's loan: days, interest a year, thousandths of the raise at most
        public const int ClearPercent = 100;         // thousandths of the raise: misuse followed by a shutdown over this is fraud
        public const int HalfRaise = 500;            // thousandths of the raise: misuse over this is fraud whatever follows
        public const int CostsPermille = 100;        // costs the investors add to a claim
        public const int RelatedDays = 5;            // days of the stage's burn a related party deal is priced against
        public const int MisuseKept = 12, RelatedKept = 6;
        public const int RelatedGap = 30, RelatedWindow = 90, RelatedShare = 150;   // days between deals with one business; the window and the share (thousandths) of its burn they may total
        public const int RoutineGrace = 30, RoutineWeight = 3;   // thousandths of the raise that running-cost gaps may total before they count towards suspicion, and the fraction that then counts
        public const long NoiseCents = 5000;         // a payment under this is never counted against the floor
        public const int ScansPerDay = 200;          // looks at the game's transactions a day, at most
        public const int ExplainBase = 450;          // thousandths: the chance an explanation is believed before skills and size
        public const int StandingFall = 3;           // integrity points lost when the board removes the founder

        /// <summary>A founder's salary a month the board allows, by the stage just raised.</summary>
        public static readonly long[] SalaryBand = { 400000, 600000, 900000, 1400000, 2000000, 2800000 };
    }

    /// <summary>The rules of the firm account, the board's view of it and the ladder of what the board does. Game-free.</summary>
    public static class StartupMoney
    {
        public const int StepNone = 0, StepAsked = 1, StepDemand = 2, StepRemoved = 3, StepClaim = 4, StepCharged = 5;
        public const int ActNone = 0, ActAsk = 1, ActDemand = 2, ActRemove = 3, ActClaim = 4, ActCharge = 5;

        /// <summary>A fixed hash of a name for a roll: the same on every machine (string.GetHashCode is not).</summary>
        public static int Hash(string text)
        {
            uint h = 2166136261;
            for (int i = 0; text != null && i < text.Length; i++)
            {
                h = (h ^ text[i]) * 16777619;
            }
            return (int)(h & 0x7FFFFFFF);
        }

        public static bool Has(Cap c) => c != null && c.Acc.Open && !c.Acc.Wound;

        /// <summary>Who is owed from a close or a sale: the venture firms and the public a listing sold shares to.</summary>
        public static bool IsInvestor(int kind) => kind == (int)HolderKind.Vc || kind == (int)HolderKind.Public;

        /// <summary>What the firm holds by the ledger: raised, plus its own takings less costs, less what was lawfully taken out.</summary>
        public static long Balance(Cap c) => c.Acc.Raised + c.Acc.Own - c.Acc.Out;

        /// <summary>The part of the balance that is the investors': what they put in, as far as the firm still holds it.</summary>
        public static long Investors(Cap c) => Math.Max(0L, Math.Min(Balance(c), c.Acc.Raised));

        /// <summary>What the player's wallet must keep for this firm: the investors' part less what has already been counted as taken. Nothing in an old save's grace.</summary>
        public static long Floor(Cap c, int day)
        {
            FirmAccount a = c.Acc;
            if (!a.Open || a.Wound || day <= a.GraceTo)
            {
                return 0L;
            }
            return Math.Max(0L, Investors(c) - a.Taken);
        }

        public static long FloorOf(IList<Cap> caps, int day)
        {
            long sum = 0L;
            for (int i = 0; i < caps.Count; i++)
            {
                sum += Floor(caps[i], day);
            }
            return sum;
        }

        public static long Band(int stage) => MoneyCatalogue.SalaryBand[Math.Max(0, Math.Min(MoneyCatalogue.SalaryBand.Length - 1, stage))];

        /// <summary>A round or a bridge taken: the account opens with the first, and every round sets the salary band for its stage.</summary>
        public static void Inflow(Cap c, long cents, int day)
        {
            FirmAccount a = c.Acc;
            if (!a.Open)
            {
                a.Open = true;
                a.Opened = day;
                a.FounderDay = day;
                a.Synced = day;
                a.PackDay = day + MoneyCatalogue.PackDays;
            }
            a.Raised += Math.Max(0L, cents);
            a.Band = Band(c.Stage);
        }

        /// <summary>A firm that already had investors in a save from before the account: it opens with nothing raised on the books, no floor, and a grace.</summary>
        public static bool OpenOld(Cap c, int day, int year)
        {
            if (c.Acc.Open || (c.VcSeats <= 0 && !HasInvestor(c)))
            {
                return false;
            }
            FirmAccount a = c.Acc;
            a.Open = true;
            a.Opened = day;
            a.GraceTo = day + MoneyCatalogue.GraceDays;
            a.FounderDay = day - VentureCatalogue.VestYears * Math.Max(60, year);
            a.Synced = day;
            a.PackDay = day + MoneyCatalogue.PackDays;
            a.Band = Band(c.Stage);
            return true;
        }

        public static bool HasInvestor(Cap c)
        {
            for (int i = 0; i < c.Holders.Count; i++)
            {
                if (c.Holders[i].Kind == (int)HolderKind.Vc)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>The firm's own profit for the days since the last look, from the game's statements (a loss is negative).</summary>
        public static void Profit(Cap c, long cents, int day)
        {
            c.Acc.Own += cents;
            c.Acc.Synced = day;
        }

        /// <summary>Money spent on the firm at its own address that is no running cost of the statement (a fit out, a deposit): the account is lower by it.</summary>
        public static void Spent(Cap c, long cents)
        {
            c.Acc.Own -= Math.Max(0L, cents);
        }

        // ---------------------------------------------------------------- the floor

        /// <summary>
        /// The wallet fell by <paramref name="drop"/> to <paramref name="wallet"/> and is under the floor: what is under it, up to the drop,
        /// is counted as investors' money spent elsewhere, firm by firm, the largest floor first. Returns the cents counted.
        /// </summary>
        public static long Shortfall(IList<Cap> caps, long wallet, long drop, int day, bool burning, string key, bool routine = false, string name = "")
        {
            long left = Math.Min(Math.Max(0L, drop), Math.Max(0L, FloorOf(caps, day) - wallet)), counted = 0L;
            while (left > 0)
            {
                Cap best = null;
                long most = 0L;
                for (int i = 0; i < caps.Count; i++)
                {
                    long f = Floor(caps[i], day);
                    if (f > most)
                    {
                        most = f;
                        best = caps[i];
                    }
                }
                if (best == null)
                {
                    break;
                }
                long take = Math.Min(left, most);
                Misuse(best, take, day, burning, key, routine, name);
                left -= take;
                counted += take;
            }
            return counted;
        }

        public static void Misuse(Cap c, long cents, int day, bool burning, string key, bool routine = false, string name = "")
        {
            FirmAccount a = c.Acc;
            a.Taken += cents;
            a.Event = day;
            a.Calm = 0;
            name = name ?? string.Empty;
            if (a.Misuse.Count > 0 && a.Misuse[a.Misuse.Count - 1].Day == day && a.Misuse[a.Misuse.Count - 1].Key == key && a.Misuse[a.Misuse.Count - 1].Name == name)
            {
                a.Misuse[a.Misuse.Count - 1].Cents += cents;
                return;
            }
            if (a.Misuse.Count >= MoneyCatalogue.MisuseKept)
            {
                a.Misuse[1].Cents += a.Misuse[0].Cents;   // the oldest two become one line, so the lines still add up to what was taken
                a.Misuse.RemoveAt(0);
            }
            a.Misuse.Add(new MisuseLine { Day = day, Cents = cents, Burning = burning, Routine = routine, Key = key ?? string.Empty, Name = name });
        }

        /// <summary>The player puts free cash back: counted against what was taken, oldest first. Returns the cents put back.</summary>
        public static long Repay(Cap c, long free, long want, int day)
        {
            FirmAccount a = c.Acc;
            long pay = Math.Min(Math.Min(Math.Max(0L, free), Math.Max(0L, want)), a.Taken);
            if (pay <= 0)
            {
                return 0L;
            }
            a.Taken -= pay;
            long left = pay;
            while (left > 0 && a.Misuse.Count > 0)
            {
                MisuseLine m = a.Misuse[0];
                long cut = Math.Min(left, m.Cents);
                m.Cents -= cut;
                left -= cut;
                if (m.Cents <= 0)
                {
                    a.Misuse.RemoveAt(0);
                }
            }
            if (a.Taken <= 0)
            {
                a.Taken = 0;
                a.Misuse.Clear();
                a.Stonewalled = false;
                a.Step = a.Step >= StepRemoved ? a.Step : StepNone;
            }
            a.Calm = day + MoneyCatalogue.PackDays;
            a.Event = day;
            return pay;
        }

        // ---------------------------------------------------------------- lawful ways out

        /// <summary>The salary the band still allows this month.</summary>
        public static long SalaryLeft(Cap c, int day)
        {
            FirmAccount a = c.Acc;
            if (!a.Open || a.Wound)
            {
                return 0L;
            }
            long drawn = day - a.SalaryFrom >= MoneyCatalogue.PackDays ? 0L : a.SalaryDrawn;
            return Math.Max(0L, Math.Min(a.Band - drawn, Math.Max(0L, Balance(c))));
        }

        /// <summary>Salary drawn, at most what the band allows. Returns the cents drawn.</summary>
        public static long Draw(Cap c, long want, int day)
        {
            FirmAccount a = c.Acc;
            long pay = Math.Min(Math.Max(0L, want), SalaryLeft(c, day));
            if (pay <= 0)
            {
                return 0L;
            }
            if (day - a.SalaryFrom >= MoneyCatalogue.PackDays)
            {
                a.SalaryFrom = day;
                a.SalaryDrawn = 0L;
            }
            a.SalaryDrawn += pay;
            a.Out += pay;
            return pay;
        }

        public static long LoanMax(Cap c) => c.Acc.Raised * MoneyCatalogue.LoanShare / 1000;

        /// <summary>A director's loan, once: out of the account, owed back in 180 days with interest. Returns the cents lent.</summary>
        public static long Lend(Cap c, int day)
        {
            FirmAccount a = c.Acc;
            long pay = Math.Min(LoanMax(c), Math.Max(0L, Balance(c)));
            if (!a.Open || a.Wound || a.LoanUsed || a.Loan > 0 || pay <= 0)
            {
                return 0L;
            }
            a.Loan = pay;
            a.LoanDay = day;
            a.LoanUsed = true;
            a.Out += pay;
            return pay;
        }

        public static long LoanOwed(Cap c, int day)
        {
            FirmAccount a = c.Acc;
            return a.Loan + a.Loan * MoneyCatalogue.LoanBp * Math.Max(0, day - a.LoanDay) / 365 / 10000;
        }

        /// <summary>The loan paid back with its interest out of free cash. False if the cash does not cover it.</summary>
        public static bool RepayLoan(Cap c, long free, int day)
        {
            FirmAccount a = c.Acc;
            long owed = LoanOwed(c, day);
            if (a.Loan <= 0 || free < owed)
            {
                return false;
            }
            a.Out -= a.Loan;
            a.Own += owed - a.Loan;
            a.Loan = 0L;
            return true;
        }

        /// <summary>A loan left unpaid past its days becomes misuse: the floor comes back up by it, and it is counted as taken.</summary>
        public static bool LoanLapsed(Cap c, int day)
        {
            FirmAccount a = c.Acc;
            if (a.Loan <= 0 || day - a.LoanDay <= MoneyCatalogue.LoanDays)
            {
                return false;
            }
            long owed = LoanOwed(c, day);
            a.Out -= a.Loan;
            a.Loan = 0L;
            Misuse(c, owed, day, false, "reallife_vc_misuse_loan");
            return true;
        }

        /// <summary>What a dividend may be: the firm's profit not yet paid out, and none while anything is owed to the board.</summary>
        public static long DividendRoom(Cap c, long profitToday)
        {
            FirmAccount a = c.Acc;
            if (!a.Open || a.Wound || profitToday <= 0 || a.Taken > 0 || a.Step > StepNone || a.Loan > 0)
            {
                return 0L;
            }
            return Math.Max(0L, Math.Min(a.Own - a.Dividends, Balance(c)));
        }

        /// <summary>The board votes: it agrees to a dividend unless it has just missed its target and the investors hold the board.</summary>
        public static bool DividendVote(Cap c)
        {
            return (c.Misses == 0 && c.Pressure == 0) || c.VcSeats * 2 <= c.BoardSeats;
        }

        /// <summary>A dividend paid to every holder by their share: the account is lower by all of it. Returns the investors' part, which leaves the wallet.</summary>
        public static long Dividend(Cap c, long total)
        {
            long share = VentureRules.PlayerShare(c);
            c.Acc.Out += total;
            c.Acc.Dividends += total;
            return total * (1000 - share) / 1000;
        }

        /// <summary>The mod's figure for a unit of work between the player's businesses: so many days of what the stage burns. Priced fair at this.</summary>
        public static long Fair(Cap c) => VentureCatalogue.BurnByStage[Math.Max(0, Math.Min(VentureCatalogue.BurnByStage.Length - 1, c.Stage))] * MoneyCatalogue.RelatedDays;

        /// <summary>
        /// A deal with another business of the player's, paid by the firm. What is paid over the fair price is counted as taken; the firm's
        /// costs are the fair price alone, so the floor falls by the work and no more. Returns the cents over.
        /// </summary>
        public static long Related(Cap c, long paid, long fair, string name, int day)
        {
            int allowed = RelatedCheck(c, name, paid, day);
            if (allowed == RelatedSoon || allowed == RelatedNo)
            {
                return 0L;
            }
            FirmAccount a = c.Acc;
            long over = Math.Max(0L, paid - fair);
            if (allowed == RelatedVote)
            {
                a.CapDay = day;
            }
            a.RelatedDeals++;
            a.RelatedPaid += paid;
            a.RelatedOver += over;
            if (a.Related.Count >= MoneyCatalogue.RelatedKept)
            {
                a.Related.RemoveAt(0);
            }
            a.Related.Add(new RelatedLine { Day = day, Paid = paid, Fair = fair, Name = name ?? string.Empty });
            if (over > 0)
            {
                a.Own += over;
                Misuse(c, over, day, false, "reallife_vc_misuse_related");
            }
            return over;
        }

        public const int RelatedOk = 0, RelatedSoon = 1, RelatedNo = 2, RelatedVote = 3;

        /// <summary>
        /// Whether a deal with another business of the player's may be made: at most one a business every 30 days, and all together over 90 days
        /// a share of the firm's burn. Over that the board votes: it agrees to one such deal in 90 days, and only for a firm with nothing taken,
        /// no question open and a lead that is not strict; otherwise it refuses.
        /// </summary>
        public static int RelatedCheck(Cap c, string name, long paid, int day)
        {
            FirmAccount a = c.Acc;
            long window = 0L;
            for (int i = 0; i < a.Related.Count; i++)
            {
                RelatedLine r = a.Related[i];
                if (r.Name == name && day - r.Day < MoneyCatalogue.RelatedGap)
                {
                    return RelatedSoon;
                }
                window += day - r.Day < MoneyCatalogue.RelatedWindow ? r.Paid : 0L;
            }
            long cap = VentureCatalogue.BurnByStage[Math.Max(0, Math.Min(VentureCatalogue.BurnByStage.Length - 1, c.Stage))] * MoneyCatalogue.RelatedWindow * MoneyCatalogue.RelatedShare / 1000;
            if (window + paid <= cap)
            {
                return RelatedOk;
            }
            bool agrees = a.Taken == 0 && a.Step == StepNone && (a.CapDay == 0 || day - a.CapDay >= MoneyCatalogue.RelatedWindow) && Diligence(c) < 500;
            return agrees ? RelatedVote : RelatedNo;
        }

        // ---------------------------------------------------------------- what the board sees

        /// <summary>The lead investor's diligence, 0 to 1000: the strictest of the firms on the cap table.</summary>
        public static int Diligence(Cap c)
        {
            int best = 0;
            for (int i = 0; i < c.Holders.Count; i++)
            {
                if (c.Holders[i].Kind == (int)HolderKind.Vc)
                {
                    best = Math.Max(best, VentureCatalogue.Firms[Math.Max(0, Math.Min(VentureCatalogue.FirmCount - 1, c.Holders[i].Who))].Diligence);
                }
            }
            return best;
        }

        /// <summary>The suspicion at which the lead looks into the books: a stricter firm looks sooner. Always between a question's 30 and removal's 60.</summary>
        public static int Threshold(int diligence) => 55 - Math.Max(0, Math.Min(1000, diligence)) * 25 / 1000;

        /// <summary>
        /// The board's suspicion of a firm, 0 to 100. Taken money by its size against the raise is the main part; deals over the fair
        /// price, spending while the firm burns, a delayed pack and a stonewalled question add; time and paying back bring it down,
        /// the player's Accounting softens a small shortfall and Leadership a late pack (both 0 to 100).
        /// </summary>
        public static int Suspicion(Cap c, int day, int accounting, int leadership)
        {
            FirmAccount a = c.Acc;
            if (!a.Open || a.Wound || day <= a.GraceTo)
            {
                return 0;
            }
            long routine = 0L;
            int burning = 0;
            for (int i = 0; i < a.Misuse.Count; i++)
            {
                burning += a.Misuse[i].Burning && !a.Misuse[i].Routine ? 1 : 0;
                routine += a.Misuse[i].Routine ? a.Misuse[i].Cents : 0L;
            }
            long counted = a.Taken - routine + Math.Max(0L, routine - a.Raised * MoneyCatalogue.RoutineGrace / 1000) / MoneyCatalogue.RoutineWeight;
            int size = (int)Math.Min(1000L, counted * 1000 / Math.Max(1L, a.Raised));
            int s = Math.Min(70, size * 14 / 100);
            s += a.RelatedOver > 0 ? Math.Min(10, 3 + a.RelatedDeals) : 0;
            int deals = Math.Min(10, (int)Math.Min(1000L, a.RelatedPaid * 1000 / Math.Max(1L, a.Raised)) / 20);   // many deals with the player's other businesses, even at the fair price
            s += Math.Min(15, burning * 5);
            bool late = a.DelayDay > 0 && day - a.DelayDay < 90;
            bool missed = c.Misses > 0 || c.Pressure > 0;
            s += late ? 8 : 0;
            s += missed ? 5 : 0;
            s += a.Stonewalled ? 15 : 0;
            if (a.Taken <= 0)
            {
                return Math.Max(0, Math.Min(100, s / 4 + deals));
            }
            s += deals;
            s -= Math.Min(30, Math.Max(0, day - a.Event) / 4);
            s -= size < 100 ? Math.Max(0, Math.Min(100, accounting)) * 12 / 100 : 0;
            s -= late || missed ? Math.Max(0, Math.Min(100, leadership)) * 8 / 100 : 0;
            return Math.Max(0, Math.Min(100, s));
        }

        /// <summary>How the board reads the suspicion: 0 calm, 1 uneasy, 2 asking, 3 alarmed.</summary>
        public static int Mood(int suspicion, int threshold) => suspicion >= 60 ? 3 : suspicion >= threshold ? 2 : suspicion >= 15 ? 1 : 0;

        /// <summary>The share of what was taken that a look at the books finds, thousandths; an audit finds all of it.</summary>
        public static int LookFinds(int diligence) => 400 + Math.Max(0, Math.Min(1000, diligence)) * 600 / 1000;

        public static bool Audit(Cap c, int suspicion, int threshold) => c.Acc.Step >= StepRemoved || suspicion >= threshold + 15;

        /// <summary>
        /// What the board does today: nothing, asks (on a pack day, or at once if alarmed), demands (the question unanswered), removes the
        /// founder (alarmed, or the demand ignored), sues, or reports the fraud. A firm with nothing taken never leaves nothing.
        /// </summary>
        public static int Next(Cap c, int day, int suspicion, int threshold, bool packDay)
        {
            FirmAccount a = c.Acc;
            if (!a.Open || a.Wound || day <= a.GraceTo || a.Taken <= 0)
            {
                return ActNone;
            }
            switch (a.Step)
            {
                case StepNone:
                    if (suspicion >= 60)
                    {
                        return ActRemove;
                    }
                    return day >= a.Calm && suspicion >= threshold && packDay ? ActAsk : ActNone;
                case StepAsked:
                    return suspicion >= 60 ? ActRemove : day - a.AskDay >= MoneyCatalogue.QuestionDays ? ActDemand : ActNone;
                case StepDemand:
                    return suspicion >= 60 || day - a.StepDay >= MoneyCatalogue.DemandDays ? ActRemove : ActNone;
                case StepRemoved:
                    return day - a.StepDay >= MoneyCatalogue.ClaimWaitDays ? ActClaim : ActNone;
                case StepClaim:
                    return Clear(c, MoneyCatalogue.HalfRaise) && a.Case == 0 ? ActCharge : ActNone;
                default:
                    return ActNone;
            }
        }

        /// <summary>Taken is at least so many thousandths of the raise.</summary>
        public static bool Clear(Cap c, int thousandths) => c.Acc.Taken > 0 && c.Acc.Taken * 1000 >= c.Acc.Raised * thousandths;

        /// <summary>The chance, in thousandths, that the player's explanation to the board is believed: a slip is, a large sum is not.</summary>
        public static int ExplainChance(Cap c, int accounting, int law)
        {
            int size = (int)Math.Min(1000L, c.Acc.Taken * 1000 / Math.Max(1L, c.Acc.Raised));
            int chance = MoneyCatalogue.ExplainBase + Math.Max(0, Math.Min(100, accounting)) * 3 + Math.Max(0, Math.Min(100, law)) * 2 - size * 2;
            return Math.Max(50, Math.Min(900, chance));
        }

        /// <summary>The unvested part of the founder's shares is taken back when the board removes them. Returns the shares cancelled.</summary>
        public static long BadLeaver(Cap c, int day, int year)
        {
            Holder me = null;
            for (int i = 0; i < c.Holders.Count && me == null; i++)
            {
                me = c.Holders[i].Kind == (int)HolderKind.Player ? c.Holders[i] : null;
            }
            if (me == null)
            {
                return 0L;
            }
            int vested = VentureRules.Vested(c.Acc.FounderDay, day, year);
            long back = me.Shares * (1000 - vested) / 1000;
            me.Shares -= back;
            return back;
        }

        // ---------------------------------------------------------------- the board pack

        /// <summary>The board pack is due: on its day, or later when it was delayed.</summary>
        public static bool PackDue(Cap c, int day) => c.Acc.Open && !c.Acc.Wound && c.Acc.PackDay > 0 && day >= c.Acc.PackDay;

        /// <summary>The player delays the pack, once; it counts as a warning sign. False if it was delayed already.</summary>
        public static bool Delay(Cap c, int day)
        {
            FirmAccount a = c.Acc;
            if (!a.Open || a.Wound || a.DelayUsed)
            {
                return false;
            }
            a.DelayUsed = true;
            a.PackDay += MoneyCatalogue.PackDelayDays;
            a.DelayDay = day;
            a.Event = day;
            return true;
        }

        /// <summary>The pack goes out: the next falls due a month on.</summary>
        public static void Sent(Cap c, int day)
        {
            c.Acc.PackLast = day;
            c.Acc.PackDay = day + MoneyCatalogue.PackDays;
            c.Acc.DelayUsed = false;
        }

        // ---------------------------------------------------------------- the wind down

        /// <summary>What a funded firm's closing comes to.</summary>
        public struct Wind
        {
            public long Price;       // the firm's cash that is shared: the balance less the bridge
            public long Bridge;      // the bridge loan, paid first
            public long Owed;        // to the investors in all: the bridge and their lines of the waterfall
            public long Paid;        // out of the wallet
            public long Gap;         // owed and not paid: the investors' claim
            public long Taken;       // misuse standing at the close
            public bool Fraud;       // misuse over a tenth of the raise and a gap: the clear pattern
            public bool Dry;         // the firm honestly ran dry: nothing was left to share
        }

        public static Wind Close(Cap c, long wallet, int day)
        {
            var w = new Wind { Taken = c.Acc.Taken };
            long balance = Math.Max(0L, Balance(c));
            w.Bridge = Math.Min(Math.Max(0L, c.Bridge), balance);
            w.Price = balance - w.Bridge;
            w.Dry = balance <= 0;
            long[] pay = VentureRules.Waterfall(c.Holders, w.Price);
            w.Owed = w.Bridge;
            for (int i = 0; i < c.Holders.Count; i++)
            {
                w.Owed += IsInvestor(c.Holders[i].Kind) ? pay[i] : 0L;
            }
            w.Paid = Math.Min(w.Owed, Math.Max(0L, wallet));
            w.Gap = w.Owed - w.Paid;
            w.Fraud = w.Gap > 0 && Clear(c, MoneyCatalogue.ClearPercent);
            return w;
        }

        /// <summary>The audit's evidence for the court, 0 to 1: the sum in front of it against the raise, and how many transactions the books name.</summary>
        public static float Evidence(Cap c)
        {
            int size = (int)Math.Min(1000L, c.Acc.Taken * 1000 / Math.Max(1L, c.Acc.Raised));
            int e = 450 + size / 4 + Math.Min(5, c.Acc.Misuse.Count) * 40;
            return Math.Max(0, Math.Min(950, e)) / 1000f;
        }
    }
}
