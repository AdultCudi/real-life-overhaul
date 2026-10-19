using System;
using System.Collections.Generic;

namespace RealLifeOverhaul
{
    // This file, the LifeEventCatalogue.*.cs content files and LifeEventDeck.cs deliberately use
    // nothing from Unity or the game: tools\CatalogueCheck compiles them on their own to verify
    // the catalogue against Locales\en.json. Everything that touches the game is in LifeEventRunner.

    /// <summary>Conditions an event can ask for. All flags of an event must hold at once.</summary>
    [Flags]
    internal enum Need
    {
        None = 0,
        OnFoot = 1 << 0,         // walking outdoors
        Indoors = 1 << 1,
        Driving = 1 << 2,
        NotDriving = 1 << 3,
        Rain = 1 << 4,
        Dry = 1 << 5,
        Weekend = 1 << 6,        // the game counts Friday to Sunday
        Weekday = 1 << 7,
        Monday = 1 << 8,
        Friday = 1 << 9,
        Saturday = 1 << 10,
        Sunday = 1 << 11,
        Healthy = 1 << 12,
        Sick = 1 << 13,
        Tired = 1 << 14,         // energy below 35
        Rested = 1 << 15,        // energy 60 or more
        Hungry = 1 << 16,        // hunger below 35
        Fed = 1 << 17,           // hunger 60 or more
        Happy = 1 << 18,         // happiness 60 or more
        Unhappy = 1 << 19,       // happiness below 35
        HasVehicle = 1 << 20,
        NoVehicle = 1 << 21,
        HasBusiness = 1 << 22,
        MomGiftDue = 1 << 23,    // a week since Mom last sent money
        Friend = 1 << 24,        // the phone contact exists
        Mom = 1 << 25,
        CanPrompt = 1 << 26,     // no confirm box open already
        EnergyReserve = 1 << 27, // enough energy that losing some can't knock the player out
        Newcomer = 1 << 28,      // first week in the city
        Veteran = 1 << 29,       // day 15 onwards
        HasHome = 1 << 30        // a residential building rented or owned by the player
    }

    internal enum Tone { Good, Info, Warn, Bad }

    internal enum Via { Toast, Friend, Mom }

    internal enum Mood { None, Up, UpBig, Down, DownBig, Lucky, Generous, Robbed, Social, Family, Helped }

    /// <summary>One thing that can happen: a line of text plus its effects.</summary>
    internal sealed class Outcome
    {
        public string Suffix = "";
        public readonly Via Via;
        public readonly Tone Tone;
        public int MoneyMin;      // signed dollars; with MoneyPct set: the smallest amount
        public int MoneyMax;      // signed dollars; with MoneyPct set: the cap
        public float MoneyPct;    // signed share of the player's money, 0 for a plain range
        public string Txn;        // transaction key without the "reallife_transaction_" prefix
        public float Energy;
        public float Hunger;
        public Mood Mood;
        public int SickMin;
        public int SickMax;
        public int HurtHours;
        public float HurtEnergy;
        public bool Cures;
        public float Odds = 1f;   // weight among the outcomes it is listed with

        public Outcome(Via via, Tone tone)
        {
            Via = via;
            Tone = tone;
        }

        public bool HasMoney => MoneyMin != 0 || MoneyMax != 0 || MoneyPct != 0f;
        public bool DrainsEnergy => Energy < 0f || HurtHours > 0;

        /// <summary>The most this outcome can cost the player, 0 if it never costs anything.</summary>
        public int MaxLoss => MoneyPct < 0f ? MoneyMin : (MoneyPct == 0f && MoneyMin < 0 ? -MoneyMin : 0);

        /// <summary>A random amount between min and max, both gains or both losses (negative).</summary>
        public Outcome Money(int min, int max, string txn)
        {
            MoneyMin = Math.Min(min, max);
            MoneyMax = Math.Max(min, max);
            Txn = txn;
            return this;
        }

        /// <summary>A share of the player's money (negative to lose it), at least min and at most cap.</summary>
        public Outcome Pct(float pct, int min, int cap, string txn)
        {
            MoneyPct = pct;
            MoneyMin = min;
            MoneyMax = cap;
            Txn = txn;
            return this;
        }

        public Outcome Tire(float energy) { Energy = -energy; return this; }
        public Outcome Rest(float energy) { Energy = energy; return this; }
        public Outcome Feed(float hunger) { Hunger = hunger; return this; }
        public Outcome Feel(Mood mood) { Mood = mood; return this; }
        public Outcome Up() => Feel(Mood.Up);
        public Outcome UpBig() => Feel(Mood.UpBig);
        public Outcome Down() => Feel(Mood.Down);
        public Outcome DownBig() => Feel(Mood.DownBig);
        public Outcome Sick(int minHours, int maxHours) { SickMin = minHours; SickMax = maxHours; return this; }
        public Outcome Hurt(int hours, float energy) { HurtHours = hours; HurtEnergy = energy; return this; }
        public Outcome Cure() { Cures = true; return this; }
        public Outcome Chance(float odds) { Odds = odds; return this; }
    }

    /// <summary>What the game looks like right now, as far as events care.</summary>
    internal struct LifeContext
    {
        public Need Flags;
        public int Hour;
        public float Money;
    }

    /// <summary>
    /// A catalogue entry. Plain events pick one of Outcomes; choices show a two-button prompt and
    /// pick one of Yes or No. Text keys follow from the id: reallife_ev_{id} plus a suffix.
    /// </summary>
    internal sealed class LifeEvent
    {
        public const string KeyPrefix = "reallife_ev_";
        public const string TransactionPrefix = "reallife_transaction_";

        private const Need Situational = Need.OnFoot | Need.Indoors | Need.Driving | Need.Rain | Need.Monday | Need.Friday
                                         | Need.Saturday | Need.Sunday | Need.Sick | Need.Tired | Need.Hungry | Need.Unhappy
                                         | Need.Newcomer;

        public readonly string Id;
        public readonly bool IsChoice;
        public readonly int Cost;            // paid when a choice is confirmed, at the start's prices (CostNow is today's)
        public readonly string CostTxn;
        public readonly Outcome[] Outcomes;  // plain events
        public readonly Outcome[] Yes;       // choices
        public readonly Outcome[] No;        // choices; may be empty

        public Need Needs { get; private set; }
        public int FromHour { get; private set; } = -1;
        public int ToHour { get; private set; } = -1;
        public int MinMoney { get; private set; }

        /// <summary>The choice's price at today's prices (the catalogue's figures are the start's: Core/Prices).</summary>
        public int CostNow => Prices.At(Cost);

        /// <summary>
        /// The sum a choice's texts quote besides its price ({sum}): the first fixed payout among the yes answer's outcomes
        /// (a stranger's odd job, a bet's winnings, the gift card that is now and then real), so that the text and the money
        /// are one figure. Nothing when there is none.
        /// </summary>
        public int Quote { get; private set; }

        public int QuoteNow => Prices.At(Quote);
        public int MaxMoney { get; private set; } = -1;
        public float Weight { get; private set; } = 1f;

        public LifeEvent(string id, Need needs, Outcome[] outcomes)
        {
            Id = id;
            Needs = needs;
            Outcomes = outcomes;
            Yes = No = Array.Empty<Outcome>();
            Label(outcomes, outcomes.Length == 1 ? null : "_");
            Derive();
        }

        public LifeEvent(string id, Need needs, int cost, string costTxn, Outcome[] yes, Outcome[] no)
        {
            Id = id;
            IsChoice = true;
            Cost = cost;
            CostTxn = costTxn;
            Outcomes = Array.Empty<Outcome>();
            Yes = yes;
            No = no;
            // A prompt in the middle of traffic would be unfair, so choices wait until the player has stopped.
            Needs = needs | Need.NotDriving | Need.CanPrompt;
            Label(yes, "_y");
            Label(no, "_n");
            Derive();
        }

        public string Category => Id.Substring(0, 2);
        public string Key => KeyPrefix + Id;
        public string HeaderKey => Key + "_h";
        public string BodyKey => Key + "_b";
        public string ConfirmKey => Key + "_y";
        public string CancelKey => Key + "_n";

        public IEnumerable<Outcome> AllOutcomes
        {
            get
            {
                foreach (Outcome o in Outcomes) yield return o;
                foreach (Outcome o in Yes) yield return o;
                foreach (Outcome o in No) yield return o;
            }
        }

        public string TextKey(Outcome outcome) => Key + outcome.Suffix;

        /// <summary>Every locale key this event can show (not counting transaction labels).</summary>
        public IEnumerable<string> TextKeys()
        {
            if (IsChoice)
            {
                yield return HeaderKey;
                yield return BodyKey;
                yield return ConfirmKey;
                yield return CancelKey;
            }
            foreach (Outcome o in AllOutcomes)
            {
                yield return TextKey(o);
            }
        }

        public IEnumerable<string> TransactionKeys()
        {
            if (Cost > 0)
            {
                yield return TransactionPrefix + CostTxn;
            }
            foreach (Outcome o in AllOutcomes)
            {
                if (o.HasMoney)
                {
                    yield return TransactionPrefix + o.Txn;
                }
            }
        }

        /// <summary>Only between these hours; from may be later than to for a span over midnight.</summary>
        public LifeEvent At(int fromHour, int toHour) { FromHour = fromHour; ToHour = toHour; return this; }
        public LifeEvent Rich(int minMoney) { MinMoney = Math.Max(MinMoney, minMoney); return this; }
        public LifeEvent Poor(int maxMoney) { MaxMoney = maxMoney; return this; }
        public LifeEvent W(float weight) { Weight = weight; return this; }

        /// <summary>Only while this holds (something the event is about that the player may not have: LifeAccounts).</summary>
        public LifeEvent Only(Func<bool> test) { Test = test; return this; }

        public Func<bool> Test { get; private set; }

        public bool Fits(in LifeContext context)
        {
            // MinMoney 0 means "don't care": a player in debt still gets the events that cost nothing. (The bounds are the
            // start's, as every figure here: at today's prices.)
            if ((Needs & context.Flags) != Needs || (MinMoney > 0 && context.Money < Prices.At(MinMoney))
                || (MaxMoney >= 0 && context.Money > Prices.At(MaxMoney)) || (Test != null && !Test()))
            {
                return false;
            }
            if (FromHour < 0)
            {
                return true;
            }
            return FromHour <= ToHour
                ? context.Hour >= FromHour && context.Hour < ToHour
                : context.Hour >= FromHour || context.Hour < ToHour;
        }

        /// <summary>Events tied to a particular situation come up more readily while that situation lasts.</summary>
        public float DrawWeight
        {
            get
            {
                int specific = FromHour >= 0 ? 1 : 0;
                for (int bits = (int)(Needs & Situational); bits != 0; bits &= bits - 1)
                {
                    specific++;
                }
                return Weight * (1f + 0.6f * specific);
            }
        }

        private static void Label(Outcome[] outcomes, string stem)
        {
            for (int i = 0; i < outcomes.Length; i++)
            {
                outcomes[i].Suffix = stem == null ? "" : stem + (i + 1);
            }
        }

        // Conditions that follow from the effects, so the content files can't forget them.
        private void Derive()
        {
            int worstLoss = 0;
            foreach (Outcome o in AllOutcomes)
            {
                if (o.SickMax > 0) Needs |= Need.Healthy;
                if (o.DrainsEnergy) Needs |= Need.EnergyReserve;
                if (o.Via == Via.Friend) Needs |= Need.Friend;
                if (o.Via == Via.Mom) Needs |= Need.Mom;
                worstLoss = Math.Max(worstLoss, o.MaxLoss);
            }
            if (Cost > 0 || worstLoss > 0)
            {
                // Random events never put the player in debt: a plain loss keeps a small cushion.
                MinMoney = Cost + worstLoss + (IsChoice ? 0 : 50);
            }
            foreach (Outcome o in Yes)
            {
                if (o.MoneyPct == 0f && o.MoneyMin == o.MoneyMax && o.MoneyMin > 0)
                {
                    Quote = o.MoneyMin;
                    break;
                }
            }
        }
    }

    /// <summary>
    /// All random life events. The content is split over the LifeEventCatalogue.*.cs files by
    /// theme; ids are two letters for the theme plus a number and must never be reused, because
    /// saves remember which ids they have already seen.
    /// </summary>
    internal static partial class LifeEventCatalogue
    {
        public static readonly LifeEvent[] All = Build();
        private static readonly Dictionary<string, LifeEvent> ById = Index(All);

        public static bool Contains(string id) => ById.ContainsKey(id);

        private static LifeEvent[] Build()
        {
            var events = new List<LifeEvent>(320);
            events.AddRange(Street());
            events.AddRange(Weather());
            events.AddRange(Transport());
            events.AddRange(Home());
            events.AddRange(Business());
            events.AddRange(FriendTexts());
            events.AddRange(FamilyTexts());
            events.AddRange(Health());
            events.AddRange(MoneyMatters());
            events.AddRange(Luck());
            events.AddRange(Food());
            events.AddRange(Tech());
            events.AddRange(Animals());
            events.AddRange(WeekMoments());
            return events.ToArray();
        }

        private static Dictionary<string, LifeEvent> Index(LifeEvent[] events)
        {
            var index = new Dictionary<string, LifeEvent>(events.Length);
            foreach (LifeEvent e in events)
            {
                index[e.Id] = e;
            }
            return index;
        }

        // Shorthand for the content files.
        private static LifeEvent E(string id, Need needs, params Outcome[] outcomes) => new LifeEvent(id, needs, outcomes);

        private static LifeEvent C(string id, Need needs, int cost, string costTxn, Outcome[] yes, Outcome[] no) =>
            new LifeEvent(id, needs, cost, costTxn, yes, no);

        private static LifeEvent C(string id, Need needs, Outcome[] yes, Outcome[] no) =>
            new LifeEvent(id, needs, 0, null, yes, no);

        private static Outcome[] Yes(params Outcome[] outcomes) => outcomes;
        private static Outcome[] No(params Outcome[] outcomes) => outcomes;

        private static Outcome Good() => new Outcome(Via.Toast, Tone.Good);
        private static Outcome Info() => new Outcome(Via.Toast, Tone.Info);
        private static Outcome Warn() => new Outcome(Via.Toast, Tone.Warn);
        private static Outcome Bad() => new Outcome(Via.Toast, Tone.Bad);
        private static Outcome Alex() => new Outcome(Via.Friend, Tone.Info);
        private static Outcome Mum() => new Outcome(Via.Mom, Tone.Info);
    }
}
