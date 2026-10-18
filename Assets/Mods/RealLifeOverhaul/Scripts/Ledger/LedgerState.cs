using System.Collections.Generic;

namespace RealLifeOverhaul.Ledger
{
    /// <summary>What the morning's round came to for the subscription (<see cref="LedgerRules.Morning"/>).</summary>
    public enum LedgerMorning
    {
        None,       // no subscription, or this morning's round is done already
        Kept,       // paid for: today's paper is delivered
        Billed,     // a new week was paid for this morning, and today's paper is delivered
        Lapsed,     // the new week could not be paid: the subscription has stopped, nothing is delivered
        Ended       // a cancelled subscription has run to the last day paid for
    }

    /// <summary>Where a kiosk stands: its middle on the pavement, the way its counter faces (degrees), the building behind it.</summary>
    public struct KioskSpot
    {
        public float X, Y, Z, Heading;
        public string Building;          // the address of the building it stands in front of (it lights up on the map)
    }

    /// <summary>What the City Ledger keeps of a save (<see cref="LedgerStore"/>). Game-free.</summary>
    public sealed class LedgerState
    {
        public bool Subscribed;
        public bool Cancelled;           // it ends after the last day paid for
        public int PaidUntil;            // the last morning paid for
        public int Since;                // the day it was taken out
        public int Weeks;                // weeks paid, ever
        public int LastMorning;          // the last morning the round has been done for (a delivery, a payment)

        public int CopyDay;              // the edition's day; 0 none
        public bool Delivered;           // left inside the door of the home
        public bool Bought;              // bought at a kiosk
        public int Copies;               // copies bought, ever

        public string KiosksFor;         // the address of the home they were found near; null none
        public readonly List<KioskSpot> Kiosks = new List<KioskSpot>(LedgerCatalogue.KiosksWanted);

        public string Council;           // the council's policies as they stood at the last edition (the wiring's words); null none yet
        public LedgerEdition Edition;
    }
}
