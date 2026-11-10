using System;

namespace RealLifeOverhaul.Dynasty
{
    /// <summary>Who an heir or a bequest is, as the save writes it: never reorder, add at the end.</summary>
    public enum KinKind
    {
        None = 0,       // nobody named: the fixed order of the law decides
        Partner = 1,    // the partner family life keeps
        Child = 2,      // a child of the family, by name
        Person = 3,     // somebody the player knows (the social life's cast, by index): a relative when their origin is family
        Charity = 4     // one of Giving's charities, by its number
    }

    /// <summary>How an estate passes: never reorder.</summary>
    public enum HandMode
    {
        Death = 0,      // the last day before the game's own end in old age
        Retire = 1,     // handed over alive
        Elder = 2       // the retired founder dies: what is left of the fund they kept
    }

    /// <summary>
    /// Every number of the dynasty, each with its unit and where it comes from. Money is whole money at the start's prices (<see cref="DynastyRules.At"/> turns it into today's);
    /// a share or a rate is thousandths; an age is years of the game's own calendar. Game-free: EconomyCheck compiles it.
    /// </summary>
    public static class DynastyCatalogue
    {
        // ---- estate tax
        /// <summary>The part of an estate no tax falls on. The UK's nil rate band with the home allowance is 500,000 pounds (IHTA 1984 s.7, s.8D) and the US federal
        /// exemption is over 13 million dollars (IRC 2010); a first shop's estate passes free here and a company's does not.</summary>
        public const long Threshold = 1000000;

        /// <summary>The width of each band above the threshold but the last, which has no end.</summary>
        public static readonly long[] BandWidth = { 1000000, 4000000 };

        /// <summary>The rate of each band, thousandths: the US schedule climbs from 18% to its top rate of 40% (IRC 2001(c)), which is also the UK's one rate.</summary>
        public static readonly int[] BandRate = { 200, 300, 400 };

        /// <summary>The most of an estate a married partner's relief may cover, thousandths. Japan frees a spouse's part up to the statutory share of a half
        /// (Inheritance Tax Act art. 19-2); the US and the UK free all of it, which would let a line marry its way out of the tax.</summary>
        public const int SpouseShare = 500;

        /// <summary>Years before a death or a handover in which a gift to a person still counts as part of the estate (IRC 2035 looks back three years; the UK seven).</summary>
        public const int WindowYears = 3;

        public const int GiftsKept = 240;          // dated gifts kept at most: past it the two oldest are counted as one on the newer day, which only lengthens their time
        public const int StatementsKept = 6;       // estate statements the page keeps
        public const int KinKept = 24;             // people on the family's ledger of what was left to them
        public const int LineKept = 12;            // heads of the family remembered
        public const int NotesKept = 8;            // pieces for the paper waiting to be printed
        public const int PaperKept = 6;            // days a piece stays news

        // ---- the will
        public const int WillFee = 1200;           // whole money to have a will drawn up (a lawyer's will with an estate plan costs about a thousand dollars in the US)
        public const int ChangeFee = 300;          // ... and to change it (a codicil)
        public const int BequestsMost = 6;         // bequests in one will
        public const int ShareStep = 50;           // thousandths a share moves by
        public const int OthersMost = 500;         // thousandths of the estate all bequests together may take: the heir's reserved half (forced heirship, as France's reserve)
        public const int HeirLeast = 18;           // years: the youngest who can carry on (the family's children are grown at eighteen)
        public const int HeirMost = 85;            // years: the oldest who can
        public const int PartnerGap = 2;           // years a partner whose age nobody keeps is taken to be younger than the founder

        // ---- retiring
        public const int RetireLeast = 55;         // years: the earliest age the will can set for retiring (the earliest private pension age in the UK is 55)
        public const int RetireMost = 85;
        public const int RetireStep = 5;
        /// <summary>The share of the estate a retiring founder may keep as their own fund, thousandths: it is not passed on, so it is not taxed until they die.</summary>
        public static readonly int[] KeepSteps = { 0, 50, 100, 150, 200 };
        public const int LastAge = 90;             // years: the game's own end in old age (FuneralHelper.DeathAge); the retired founder's too

        // ---- the debt for tax the cash did not cover
        public const int LoanRate = 60;            // thousandths a year on what is still owed (the US lets estate tax on a business be paid over years with interest, IRC 6166)
        public const int LoanYears = 5;            // years it is paid over, a part a day
        public const int LoanLateMost = 30;        // days in a row a part may be missed before it is taken whatever the cash

        // ---- what the heir knows and is known for
        public const int ChildBase = 300;          // thousandths of the founder's learning a child has from growing up beside them
        public const int ChildSchool = 100;        // ... more for the public school
        public const int ChildPrivate = 250;       // ... or for a private one
        public const int ChildFund = 250;          // ... more for a college fund paid in full, in proportion
        public const int ChildClose = 200;         // ... more for a child as close as can be, in proportion
        public const int PartnerBase = 500;        // a partner who shared the working life
        public const int PartnerClose = 300;       // ... more by their affection
        public const int RelativeBase = 300;       // a relative
        public const int RelativeClose = 300;      // ... more by how close the two are
        public const int FameShare = 500;          // thousandths of the founder's recognition and press that the family name keeps

        // ---- rivals
        public const int RivalAgeFrom = 45, RivalAgeSpan = 20;       // a tycoon's age when the dynasty first looks: 45 to 64
        public const int RivalRetireFrom = 65, RivalRetireSpan = 10; // the age they retire at: 65 to 74
        public const int RivalDeathFrom = 76, RivalDeathSpan = 14;   // the age they die at: 76 to 89
        public const int RivalInOffice = 4;                          // one head in this many never retires and dies in office
        public const int RivalOfficeFrom = 68, RivalOfficeSpan = 20; // ... at 68 to 87
        public const int RivalHeirFrom = 28, RivalHeirSpan = 15;     // an heir's age when they take over: 28 to 42
        public const int RivalSlowDays = 60;                         // days a new head's fortune grows at half the pace (the rivals' own slow period)
        public const long PaperFrom = 1000000;                       // an estate's tax from which the paper prints the handover, at the start's prices

        public static int Clamp(int value, int least, int most) => value < least ? least : value > most ? most : value;

        public static long Clamp(long value, long least, long most) => value < least ? least : value > most ? most : value;
    }
}
