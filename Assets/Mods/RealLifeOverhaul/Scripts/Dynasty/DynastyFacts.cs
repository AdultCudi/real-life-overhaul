using System.Collections.Generic;

namespace RealLifeOverhaul.Dynasty
{
    /// <summary>Everything the dynasty's pages read, in plain data: the live source fills it from the director and the samples and checks fill it by hand.</summary>
    public sealed class DynastyFacts
    {
        public bool Ready = true;                  // the dynasty runs for this save
        public bool Session;                       // a Going Public session: nothing changes hands
        public string Name = string.Empty;         // the head being played
        public int Age;                            // years
        public int Generation = 1;
        public int Level = 1000;
        public int WindowYears = DynastyCatalogue.WindowYears;
        public long Cash, Spare, Gifts;

        // ---- the will
        public Will Signed = new Will();
        public Will Draft = new Will();
        public bool Dirty;                         // the draft differs from what is signed
        public WillWhy DraftWhy;
        public long Fee;
        public string Firm = string.Empty;
        public bool OwnFirm;
        public readonly List<KinPerson> People = new List<KinPerson>(16);

        // ---- the estate today
        public Statement Today;                    // the estate if it passed today; null with nobody to value it
        public bool HeirKnown;
        public RetireWhy Retire = RetireWhy.Off;
        public Statement RetireToday;              // ... and if the founder retired today
        public DeathHold Hold;
        public bool LastDays;

        // ---- what is under way and what is owed
        public bool Handing;
        public int Step, Steps;
        public Elder Elder;
        public int ElderAge;
        public long LoanOwed, LoanPerDay, LoanPrincipal;
        public int LoanLate;

        // ---- what has been
        public readonly List<Statement> Statements = new List<Statement>(DynastyCatalogue.StatementsKept);
        public readonly List<LineEntry> Line = new List<LineEntry>(DynastyCatalogue.LineKept);
        public readonly List<Kin> Kin = new List<Kin>(8);
        public readonly List<string> RivalLines = new List<string>(3);
        public CityEstates City = new CityEstates();
    }
}
