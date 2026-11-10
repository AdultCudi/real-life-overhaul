using System.Collections.Generic;

namespace RealLifeOverhaul.Dynasty
{
    /// <summary>One bequest of a will: who, and the share of the estate that passes, thousandths.</summary>
    public sealed class Bequest
    {
        public KinKind Kind;
        public int Id;                         // a person's index in the social cast, a charity's number; 0 for the partner or a child
        public string Name = string.Empty;     // as it was when the will was signed (a child is found by it)
        public int Share;

        public Bequest Copy() => new Bequest { Kind = Kind, Id = Id, Name = Name, Share = Share };

        public bool Same(KinKind kind, int id, string name) => Kind == kind && (kind == KinKind.Child || kind == KinKind.Partner ? Name == name : Id == id);
    }

    /// <summary>The will: who carries on, what the others are left, when the founder means to retire and what they keep then.</summary>
    public sealed class Will
    {
        public bool Written;
        public int Day;                        // the day it was last signed
        public KinKind HeirKind;               // None: the law's order
        public int HeirId;
        public string HeirName = string.Empty;
        public int RetireAge = DynastyCatalogue.RetireLeast + 2 * DynastyCatalogue.RetireStep;   // years
        public int Keep;                       // index into DynastyCatalogue.KeepSteps
        public long Fees;                      // whole money paid for it in all
        public string Firm = string.Empty;     // who drew it up
        public readonly List<Bequest> Bequests = new List<Bequest>(DynastyCatalogue.BequestsMost);

        public int Others()
        {
            int sum = 0;
            for (int i = 0; i < Bequests.Count; i++)
            {
                sum += Bequests[i].Share;
            }
            return sum;
        }

        public Bequest Find(KinKind kind, int id, string name)
        {
            for (int i = 0; i < Bequests.Count; i++)
            {
                if (Bequests[i].Same(kind, id, name))
                {
                    return Bequests[i];
                }
            }
            return null;
        }

        public Will Copy()
        {
            var w = new Will { Written = Written, Day = Day, HeirKind = HeirKind, HeirId = HeirId, HeirName = HeirName, RetireAge = RetireAge, Keep = Keep, Fees = Fees, Firm = Firm };
            for (int i = 0; i < Bequests.Count; i++)
            {
                w.Bequests.Add(Bequests[i].Copy());
            }
            return w;
        }

        /// <summary>True when the two say the same about who gets what and about retiring (the fee and the day apart).</summary>
        public bool SameTerms(Will o)
        {
            if (o == null || HeirKind != o.HeirKind || HeirId != o.HeirId || HeirName != o.HeirName || RetireAge != o.RetireAge || Keep != o.Keep || Bequests.Count != o.Bequests.Count)
            {
                return false;
            }
            for (int i = 0; i < Bequests.Count; i++)
            {
                Bequest a = Bequests[i], b = o.Bequests[i];
                if (a.Kind != b.Kind || a.Id != b.Id || a.Name != b.Name || a.Share != b.Share)
                {
                    return false;
                }
            }
            return true;
        }
    }

    /// <summary>Who draws a will up: a law firm of the city, or the founder's own (no fee).</summary>
    public struct Notary
    {
        public string Name;
        public bool Own;
    }

    /// <summary>A gift of money to a person in the founder's life, dated: it counts as part of the estate while it is inside the window.</summary>
    public struct LifeGift
    {
        public int Day;
        public long Amount;                    // whole money
    }

    /// <summary>Where a bequest lands.</summary>
    public enum Landing
    {
        None = 0,        // nowhere: it stays in the estate (nobody there to receive it, a session, nothing asked)
        Household = 1,   // the person's household as the population holds it: its debt first, then its savings
        Family = 2,      // the family's own ledger of what a relative was left
        Pot = 3          // a charity's pot (Giving)
    }

    /// <summary>One bequest as an estate settles it.</summary>
    public sealed class BequestLine
    {
        public KinKind Kind;
        public int Id;
        public string Name = string.Empty;
        public int Share;                      // thousandths asked
        public long Asked;                     // whole money the share comes to
        public long Amount;                    // ... and what is paid (less when the cash does not cover every bequest, or the receiver has no room)
        public Landing Landing;
        public bool Paid;
    }

    /// <summary>
    /// An estate as it was valued and settled, every figure whole money of its day. Net = Keep + Tax + the bequests paid + Residue, always: <see cref="DynastyRules.Settle"/> makes
    /// it and <see cref="DynastyRules.Balanced"/> says so. Kept as the estate statement.
    /// </summary>
    public sealed class Statement
    {
        public int Day;
        public HandMode Mode;
        public int Generation;                 // of the one who hands over
        public string From = string.Empty, To = string.Empty;
        public KinKind HeirKind;
        public int HeirId;
        public int HeirAge;                    // years
        public int Upbringing;                 // thousandths of the founder's learning the heir starts with
        public bool ByLaw;                     // no will named them: the fixed order did
        public bool SpouseHeir;                // the heir is the married partner (the relief is theirs)
        public int Level = 1000;               // the price level it was worked at
        public long Cash;                      // in the wallet
        public long Reserved;                  // of it, kept for the investors of the founder's firms: not the estate's
        public long Other;                     // everything else the Finance app counts, less every debt
        public long Net;                       // the estate
        public long Keep;                      // what a retiring founder keeps as their fund
        public long Gifts;                     // gifts to people inside the window, counted in
        public long Charity;                   // bequests to charities, exempt
        public long Spouse;                    // the married partner's relief
        public long Prior;                     // an earlier estate of the same founder counted with this one (the fund of a founder who dies inside the window)
        public long Taxable;
        public long Tax;
        public long TaxCash;                   // of the tax, paid from the cash
        public long Loan;                      // ... and the rest, owed with interest
        public long Paid;                      // the bequests paid, all together
        public long Residue;                   // what the heir is left, debts counted
        public int Scale = 1000;               // thousandths of every bequest the cash covered
        public bool UnderWater;                // the debts are more than the estate: no tax, no bequests, nothing written off
        public readonly List<BequestLine> Lines = new List<BequestLine>(DynastyCatalogue.BequestsMost);
    }

    /// <summary>A handover under way: its statement, and the step it has reached. Saved, so a save and a load in the middle go on at the step.</summary>
    public sealed class Handover
    {
        public readonly Statement S = new Statement();
        public int Step;                       // DynastyHand's steps
        public bool LoanIn, TaxOut, KeepOut, Passed;
    }

    /// <summary>What a relative was left, in all: it left the estate for good.</summary>
    public sealed class Kin
    {
        public KinKind Kind;
        public string Name = string.Empty;
        public long Held;                      // whole money
        public int Day;                        // the last day they were left something
    }

    /// <summary>The retired founder: an older relative of the family with a fund of their own, until the game's own last age.</summary>
    public sealed class Elder
    {
        public string Name = string.Empty;
        public int Born;                       // the day they were born on, in the game's years (may be before day 0)
        public int Since;                      // the day they retired
        public long Fund;                      // whole money they still hold
        public long Draw;                      // ... of which they spend so much a month
        public long Taxable, Tax;              // of the estate they handed over, for a death inside the window
        public int Month = -1;                 // the last month they drew
    }

    /// <summary>The debt for estate tax the cash did not cover.</summary>
    public sealed class EstateLoan
    {
        public long Principal;                 // whole money
        public long Owed;                      // cents
        public long PerDay;                    // cents
        public int Since;
        public int LastDay;                    // the last day gone through
        public int Late;                       // days in a row a part could not be paid
        public long PaidCents;

        public bool Open => Owed > 0;
    }

    /// <summary>A rival tycoon's line: the head's age, when they retire or die, and what the last handover cost.</summary>
    public sealed class RivalLine
    {
        public int Born = int.MinValue;        // the head's day of birth; MinValue until the first look
        public int RetireAge, DeathAge;
        public int Generation = 1;
        public int HandDay = -1;               // the day of the last handover
        public HandMode HandMode;
        public long HandWorth, HandTax;        // the estate then, and the tax that really left it
        public int ElderBorn = int.MinValue;   // a retired head still alive: their day of birth
        public int ElderDeath;                 // ... and the age they die at

        public bool Ready => Born != int.MinValue;
    }

    /// <summary>The estates of the city's households that ended with a death, by the game's year.</summary>
    public sealed class CityEstates
    {
        public int Year = -1;
        public int Count, Taxed;               // households ended, and those over the threshold
        public long Value, Tax, Unpaid;        // what they left, the tax by the same bands, and the debts nothing was left to pay
        public int LastCount, LastTaxed;
        public long LastValue, LastTax, LastUnpaid;
    }

    /// <summary>A head of the family as the lineage remembers them.</summary>
    public sealed class LineEntry
    {
        public string Name = string.Empty;
        public int Generation;
        public int Day;                        // the day they handed over
        public HandMode Mode;
        public long Net, Tax;
    }

    /// <summary>A handover the paper may print.</summary>
    public sealed class DynastyNote
    {
        public int Day;
        public int Who;                        // -1 the player's family, 0.. a rival's index
        public HandMode Mode;
        public int Generation;                 // of the new head
        public long Tax;
        public string From = string.Empty, To = string.Empty;
    }

    /// <summary>A child of the family seen grown, so their age goes on in the game's years after eighteen (family life stops counting there).</summary>
    public sealed class GrownChild
    {
        public string Name = string.Empty;
        public int Day;
    }

    /// <summary>Everything the dynasty keeps for a save (reallife:dynasty). Game-free. Old saves have none of it and start with no will.</summary>
    public sealed class DynastyState
    {
        public const int Rivals = 3;

        public int Seed;
        public int LastDay;                    // the day last gone through
        public int Generation = 1;             // of the head being played
        public int RetireTold;                 // the age the player was last reminded at that they may retire
        public int HeldTold;                   // the day the player was last told the last day is held back
        public bool Held;                      // the last day is being held back (a session, a cell): the estate passes as soon as that is over
        public int PaperDay;
        public Will Will = new Will();
        public Handover Hand;                  // null: none under way
        public Elder Elder;                    // null: nobody retired
        public readonly EstateLoan Loan = new EstateLoan();
        public readonly CityEstates City = new CityEstates();
        public readonly List<LifeGift> Gifts = new List<LifeGift>(32);
        public readonly List<Statement> Statements = new List<Statement>(DynastyCatalogue.StatementsKept);
        public readonly List<Kin> Kin = new List<Kin>(8);
        public readonly List<LineEntry> Line = new List<LineEntry>(DynastyCatalogue.LineKept);
        public readonly List<DynastyNote> Notes = new List<DynastyNote>(DynastyCatalogue.NotesKept);
        public readonly List<GrownChild> Grown = new List<GrownChild>(4);
        public readonly RivalLine[] RivalLines = new RivalLine[Rivals];

        public DynastyState()
        {
            for (int i = 0; i < RivalLines.Length; i++)
            {
                RivalLines[i] = new RivalLine();
            }
        }

        public Kin KinOf(KinKind kind, string name, bool make)
        {
            for (int i = 0; i < Kin.Count; i++)
            {
                if (Kin[i].Kind == kind && Kin[i].Name == name)
                {
                    return Kin[i];
                }
            }
            if (!make)
            {
                return null;
            }
            while (Kin.Count >= DynastyCatalogue.KinKept)
            {
                Kin.RemoveAt(0);
            }
            var k = new Kin { Kind = kind, Name = name ?? string.Empty };
            Kin.Add(k);
            return k;
        }

        public GrownChild GrownOf(string name)
        {
            for (int i = 0; i < Grown.Count; i++)
            {
                if (Grown[i].Name == name)
                {
                    return Grown[i];
                }
            }
            return null;
        }

        public void Keep(Statement s)
        {
            Statements.Add(s);
            while (Statements.Count > DynastyCatalogue.StatementsKept)
            {
                Statements.RemoveAt(0);
            }
        }
    }
}
