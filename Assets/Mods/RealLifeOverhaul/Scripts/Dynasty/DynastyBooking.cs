using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Dynasty
{
    /// <summary>
    /// What the estate is made of besides the cash, line by line, every figure whole money (the wiring reads each from its own feature and rounds assets down and debts up).
    /// <see cref="Wealth"/> is the Finance app's net worth: the cash, the funds, vehicles, boats and property at the price paid, the shares on the market where it runs, less the
    /// game's loans, the mod's accounts, fines, court debts and the estate's own debt. The rest is what that figure leaves out. Nothing else is counted: the stock, equipment
    /// and goodwill of the businesses have no honest price in the game and stay with the businesses.
    /// </summary>
    public struct Holdings
    {
        public long Wealth;          // the Finance app's net worth, cash included
        // counted in
        public long TaxReserve;      // set aside for the year's tax bill (Company): out of the cash, back to it when the bill comes
        public long BankCapital;     // the own bank's capital as the regulator counts it, retained earnings with it, a running bank only
        public long LoansOut;        // Giving's loans to people that are still open, as much as is left to be paid back
        public long FriendsOwe;      // what people owe the player from the social life's own loans
        // taken off
        public long CreditLoans;     // the student loan and the construction loans: credit accounts the Finance app's net worth does not take off
        public long UnpaidTaxes;     // the game's tax bill waiting to be paid, with its late fee if that has been added
        public long BackTaxes;       // the game's back taxes
        public long AuditOwed;       // the mod's back tax and fines found by an audit and not yet paid
        public long FriendsOwed;     // what the player owes people (the social life's borrowing)

        public long Assets => Math.Max(0L, TaxReserve) + Math.Max(0L, BankCapital) + Math.Max(0L, LoansOut) + Math.Max(0L, FriendsOwe);

        public long Debts => Math.Max(0L, CreditLoans) + Math.Max(0L, UnpaidTaxes) + Math.Max(0L, BackTaxes) + Math.Max(0L, AuditOwed) + Math.Max(0L, FriendsOwed);

        /// <summary>The net worth the estate is made from: the Finance app's, with what it leaves out added and taken off.</summary>
        public long NetWorth => Wealth + Assets - Debts;
    }

    /// <summary>
    /// The rules that fix how money is booked and what the law and the clock say about a handover, with no game in them, called by the director and the wiring. COST: a few
    /// operations a call.
    /// </summary>
    public static class DynastyBooking
    {
        /// <summary>
        /// Books <paramref name="cents"/> through <paramref name="book"/> (an amount in whole money and cents as a float, true if the game took it): whole money in chunks the wallet's
        /// float holds exactly (<see cref="DynastyCatalogue.ChunkMost"/>), then the cents. Stops at the first refusal. Returns the cents really booked, so a refusal creates and
        /// destroys no money: the caller knows exactly what has left or arrived.
        /// </summary>
        public static long Cents(long cents, Func<float, bool> book)
        {
            long done = 0;
            long units = cents / 100;
            while (done / 100 < units)
            {
                long chunk = Math.Min(units - done / 100, DynastyCatalogue.ChunkMost);
                if (!book((float)chunk))
                {
                    return done;
                }
                done += chunk * 100;
            }
            long rest = cents - done;
            if (rest > 0 && book((float)(rest / 100.0)))
            {
                done += rest;
            }
            return done;
        }

        /// <summary>Whole money, as <see cref="Cents"/>. Returns the whole money really booked.</summary>
        public static long Units(long units, Func<float, bool> book) => units <= 0 ? 0L : Cents(units * 100, book) / 100;
    }

    /// <summary>What the law has open against the founder, read from the justice side and the police (the wiring fills it).</summary>
    public struct LawFacts
    {
        public bool Read;              // the justice side is up; false: it could not be read, which counts as open
        public int OpenCount;          // cases and claims still open
        public bool HasWarrant, ServeWarrant, InPrison, JailPending, Probation, Suspended, DrivingSuspended;
        public int CommunityHours;
        public int WantedCharges;
        public bool HearNow;           // before the court
        public bool Staged;            // in the middle of something staged or a scene (the economy side's Held)
    }

    /// <summary>The compositions the wiring used to make by hand, game-free so the checks run the very ones the director asks.</summary>
    public static class DynastyGate
    {
        /// <summary>Anything the law still has open: retiring is refused while it is. An unknown state is open.</summary>
        public static bool LawOpen(LawFacts f)
        {
            if (!f.Read)
            {
                return true;
            }
            return f.OpenCount > 0 || f.HasWarrant || f.ServeWarrant || f.InPrison || f.JailPending || f.Probation || f.CommunityHours > 0 || f.Suspended || f.DrivingSuspended
                   || f.WantedCharges != 0;
        }

        /// <summary>Held now: in prison, before the court, or in the middle of something staged.</summary>
        public static bool Custody(LawFacts f) => f.InPrison || f.HearNow || f.Staged;

        /// <summary>A death ends the save: Permadeath is switched on and armed for it.</summary>
        public static bool Final(bool permadeath, bool armed) => permadeath && armed;

        /// <summary>Why a founder in their last days cannot be handed over now; None passes the estate at once.</summary>
        public static DeathHold Hold(bool running, bool permadeath, bool armed, bool session, LawFacts law, bool heir)
        {
            return DynastyRules.Hold(running, Final(permadeath, armed), session, Custody(law), heir);
        }
    }

    /// <summary>
    /// A figure worked out once for a day and what it stands on, so a page that is rebuilt often does not work it out again. The key is the day, the director's revision and the
    /// cash: a change to any of them is worked out afresh, a rebuild with none is answered from memory. COST: three comparisons.
    /// </summary>
    public sealed class DynastyEstateCache<T> where T : class
    {
        private T _value;
        private int _day = int.MinValue, _revision = int.MinValue;
        private long _cash = long.MinValue;

        /// <summary>Times the figure was worked out.</summary>
        public int Worked { get; private set; }

        public T Get(int day, int revision, long cash, Func<T> work)
        {
            if (_value == null || day != _day || revision != _revision || cash != _cash)
            {
                _value = work();
                _day = day;
                _revision = revision;
                _cash = cash;
                Worked++;
            }
            return _value;
        }

        public void Drop()
        {
            _value = null;
        }
    }

    /// <summary>A handover that a booking or a landing could not complete: it stops loudly where it is and goes on from there, nothing paid twice and no money made.</summary>
    public sealed class HandFault : Exception
    {
        public HandFault(string message) : base(message)
        {
        }
    }

    /// <summary>The gifts the line could not take, kept in a line of their own in the save so that none is lost.</summary>
    public static class DynastyGifts
    {
        public const string Key = "reallife:dynasty_gifts";

        private static readonly System.Globalization.CultureInfo Inv = System.Globalization.CultureInfo.InvariantCulture;

        /// <summary>
        /// A gift told while the dynasty is not running. It goes into the dynasty's own line when that can be read (or is not there yet); when it cannot (a line from a newer
        /// version, or one that is damaged) it goes into the line of its own, which is added to the gifts the next time the dynasty runs. Returns where it went.
        /// </summary>
        public static string Keep(Dictionary<string, string> data, int day, long amount)
        {
            if (data == null || amount <= 0)
            {
                return "nowhere";
            }
            DynastyState kept = DynastyStore.Load(data, out int _) ?? (data.ContainsKey(DynastyStore.Key) ? null : new DynastyState());
            if (kept != null)
            {
                DynastyRules.Gift(kept.Gifts, day, amount);
                data[DynastyStore.Key] = DynastyStore.Write(kept);
                return "line";
            }
            var gifts = new List<LifeGift>(8);
            Read(data, gifts);
            DynastyRules.Gift(gifts, day, amount);
            data[Key] = Write(gifts);
            return "side";
        }

        /// <summary>The gifts of the line of their own go into the state's and the line of their own is let go.</summary>
        public static int Merge(Dictionary<string, string> data, DynastyState into)
        {
            var gifts = new List<LifeGift>(8);
            Read(data, gifts);
            if (data != null)
            {
                data.Remove(Key);
            }
            for (int i = 0; i < gifts.Count; i++)
            {
                DynastyRules.Gift(into.Gifts, gifts[i].Day, gifts[i].Amount);
            }
            return gifts.Count;
        }

        private static void Read(Dictionary<string, string> data, List<LifeGift> into)
        {
            if (data == null || !data.TryGetValue(Key, out string text) || string.IsNullOrEmpty(text))
            {
                return;
            }
            string[] records = text.Split(';');
            for (int i = 0; i < records.Length; i++)
            {
                string[] f = records[i].Split('|');
                if (f.Length >= 2 && int.TryParse(f[0], System.Globalization.NumberStyles.Integer, Inv, out int day)
                    && long.TryParse(f[1], System.Globalization.NumberStyles.Integer, Inv, out long amount) && amount > 0)
                {
                    DynastyRules.Gift(into, day, amount);
                }
            }
        }

        private static string Write(List<LifeGift> gifts)
        {
            var t = new System.Text.StringBuilder(64);
            for (int i = 0; i < gifts.Count; i++)
            {
                t.Append(i > 0 ? ";" : string.Empty).Append(gifts[i].Day.ToString(Inv)).Append('|').Append(gifts[i].Amount.ToString(Inv));
            }
            return t.ToString();
        }
    }
}
