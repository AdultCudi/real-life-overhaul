using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Schemes
{
    /// <summary>A business of the player's as the schemes read it from the company.</summary>
    public struct SchemeShopInfo
    {
        public string Place;       // as the company writes a place
        public string Name;
        public string District;    // the game's own key of its district
        public long Average;       // an average day's takings
        public bool Cash;          // customers pay at a till
        public int Covers;         // a bit for each kind of cover in force (fire 1, theft 2, liability 4)
        public bool Mine;          // the player's own in a Going Public session
    }

    /// <summary>
    /// What the schemes ask of the rest of the mod, each with a default that leaves the scheme it serves unoffered:
    /// filled in by Wiring/Wiring.SchemeLinks. Nothing here is asked per frame: on a press, or once a game day.
    /// </summary>
    public static class SchemeBridges
    {
        public delegate bool ClaimAsk(string place, int cover, int loss, out int id, out int payout, out int payDay);

        public delegate bool ClubAsk(string district, int club, int households, int level, out long taken, out int joined);

        /// <summary>The player's businesses that take money. Default: none.</summary>
        public static Action<List<SchemeShopInfo>> Shops;

        /// <summary>An invented loss is put in as a claim on a cover in force, by the insurer's own rules. False if no policy would pay.</summary>
        public static ClaimAsk Claim;

        /// <summary>The most a kind of cover counts a loss at. Default: nothing.</summary>
        public static Func<int, int> CoverMost;

        /// <summary>True while a claim put in is still to be paid. Default: no.</summary>
        public static Func<int, bool> ClaimUnpaid;

        /// <summary>The insurer refuses a claim not yet paid, and will not write that cover there again. True if it was refused.</summary>
        public static Func<int, bool> ClaimRefuse;

        /// <summary>The game's tax rate on a business's profit, per cent, and whether it falls due at all this year. Default: no tax.</summary>
        public static Func<int> TaxPercent;

        public static Func<bool> Taxed;

        /// <summary>The game's own tax rate on a business's sales, per cent, whatever the company's pages show: taken off washed cash. Default: none.</summary>
        public static Func<int> GameTax;

        /// <summary>Starts business pressure's own audit or inspection at a place, by its own rules: an inquiry's visit. False if it would not.</summary>
        public static Func<string, bool> Visit;

        /// <summary>True while the city's people are this machine's own to take from (the living population runs here, outside a session). Default: no.</summary>
        public static Func<bool> People;

        /// <summary>A club's stakes taken from a district's households, by the draw the rules give. False if nothing could be taken.</summary>
        public static ClubAsk Club;

        /// <summary>Money goes back to the households of a business's district (the place given). Returns what they took. Default: nothing.</summary>
        public static Func<string, long, long> Restore;

        /// <summary>True while the courts take a case. Default: no court.</summary>
        public static Func<bool> Court;

        /// <summary>A file goes to the court: the charge, the evidence in thousandths, the sum it is about, the case's own seed. Its number, or 0.</summary>
        public static Func<string, int, long, int, int> Charge;

        /// <summary>How a case stands: 0 while it is open, else how it ended as an <see cref="InquiryEnd"/>. Default: open.</summary>
        public static Func<int, int> CaseEnd;

        /// <summary>The number of the criminal case that comes up next, or 0 with none. Default: none.</summary>
        public static Func<int> NextCase;

        /// <summary>A bought judge weighs the evidence of a case lightly: thousandths off every charge. True if the case was there.</summary>
        public static Func<int, int, bool> Lean;

        /// <summary>Standing: the player is exposed (true for a felony: a scandal), and integrity moves by so many points. Default: nothing.</summary>
        public static Action<bool> Exposed;

        public static Action<int> Integrity;

        /// <summary>The push of the player's before the council (its number, 0 for none); the lobbyist's fee it cost; and a bought councillor carries it. Default: no push.</summary>
        public static Func<int> PushPending;

        public static Func<long> PushFee;

        public static Func<bool> PushCarry;

        /// <summary>An invitation to the next city reception is sent. False if one is waiting already or none can be sent.</summary>
        public static Func<bool> Invite;

        /// <summary>The council's seats, each as a name that changes when the seat changes hands. Default: no council.</summary>
        public static Action<List<string>> Council;

        /// <summary>The position somebody the player knows holds: 0 none, else Networking's own number (an official 6, a law firm's partner 3). Default: none.</summary>
        public static Func<int, int> RoleOf;

        /// <summary>Somebody the player knows turned money down: the friendship cools. Default: nothing.</summary>
        public static Action<int> Cooled;

        /// <summary>The rival tycoons as they stand: who is active and what each is worth. Default: none.</summary>
        public static Action<SchemeWorld> Rivals;

        /// <summary>A rival's name and holding company. Default: null.</summary>
        public static Func<int, string> RivalName;

        public static Func<int, string> RivalHolding;

        /// <summary>A rival's worth moves by so much. Default: nothing.</summary>
        public static Action<int, long> RivalMoney;

        /// <summary>A convicted rival is barred and quiet for so many days. Default: nothing.</summary>
        public static Action<int, int, int> RivalBarred;

        /// <summary>The player's net worth. Default: the cash.</summary>
        public static Func<long> Worth;

        /// <summary>What the wallet must keep for the investors of the player's firms, whole money, never under what the firms count. Default: nothing.</summary>
        public static Func<long> Floor;

        /// <summary>A piece for the paper, by locale key. Default: nothing printed.</summary>
        public static Action<string> Story;

        /// <summary>A letter from the player's counsel, by its text's key and what fills it. Default: none is sent.</summary>
        public static Action<string, Dictionary<string, string>> Letter;

        public static void Reset()
        {
            Shops = null;
            Claim = null;
            CoverMost = null;
            ClaimUnpaid = null;
            ClaimRefuse = null;
            TaxPercent = null;
            Taxed = null;
            GameTax = null;
            Visit = null;
            People = null;
            Club = null;
            Restore = null;
            Court = null;
            Charge = null;
            CaseEnd = null;
            NextCase = null;
            Lean = null;
            Exposed = null;
            Integrity = null;
            PushPending = null;
            PushFee = null;
            PushCarry = null;
            Invite = null;
            Council = null;
            RoleOf = null;
            Cooled = null;
            Rivals = null;
            RivalName = null;
            RivalHolding = null;
            RivalMoney = null;
            RivalBarred = null;
            Worth = null;
            Floor = null;
            Story = null;
            Letter = null;
        }
    }
}
