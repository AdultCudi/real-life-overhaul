using System.Collections.Generic;

namespace RealLifeOverhaul.Ledger
{
    /// <summary>
    /// How the internet stands at the best of the player's homes, the best first (<see cref="NewsstandRules.Best"/>
    /// takes the lower). NotKept: the utilities are off, so a home counts as connected. Saved nowhere.
    /// </summary>
    public enum NetState { Online = 0, Outage, Installing, Cut, NoLine, NoHome, NotKept }

    /// <summary>What a morning came to for one online subscription (<see cref="NewsstandRules.Morning"/>).</summary>
    public enum OnlineMorning
    {
        None,       // no subscription, or this morning's edition has come already
        Loaded,     // paid for: today's edition has come to the phone
        Billed,     // a new week was paid for this morning, and today's edition has come
        Waiting,    // no connection at home: nothing comes and nothing is charged until there is one
        Lapsed,     // the new week could not be paid: the subscription has stopped
        Ended       // a cancelled subscription has run to the last day paid for
    }

    /// <summary>Why a subscription could not be taken out.</summary>
    public enum OnlineRefusal { None, NoHome, NoNet, Already, InPass, NoMoney, Off }

    /// <summary>One online subscription: a title's, or the pass for all of them. Game-free.</summary>
    public sealed class OnlineSub
    {
        public bool Subscribed;
        public bool Cancelled;           // it ends after the last day paid for
        public int PaidUntil;            // the last morning paid for
        public int Since;                // the day it was taken out
        public int Weeks;                // weeks paid, ever
        public int LastDay;              // the last morning settled: its edition came, or the days before it were let go uncharged
    }

    /// <summary>
    /// What Newsstand keeps of a save (<see cref="NewsstandStore"/>), game-free: the subscriptions, the mornings each
    /// title's edition came, the earlier editions still to be read (today's is LedgerState.Edition) and the last notices.
    /// </summary>
    public sealed class NewsstandState
    {
        public readonly OnlineSub[] Subs = new OnlineSub[NewsstandCatalogue.Subscriptions];
        public readonly List<int>[] Loaded = new List<int>[NewsstandCatalogue.Titles];   // per title, the days its edition came, oldest first
        public readonly List<LedgerEdition> Earlier = new List<LedgerEdition>(NewsstandCatalogue.KeepDays);   // oldest first
        public int NotedDay;             // the last morning the "new editions" notification was shown
        public int WaitingDay;           // the last morning the player was told the editions wait for a connection

        public NewsstandState()
        {
            for (int i = 0; i < Subs.Length; i++)
            {
                Subs[i] = new OnlineSub();
            }
            for (int i = 0; i < Loaded.Length; i++)
            {
                Loaded[i] = new List<int>(NewsstandCatalogue.KeepDays + 1);
            }
        }
    }
}
