using System;
using System.Collections.Generic;
using RealLifeOverhaul.Dynasty;
using RealLifeOverhaul.Ui;

// The Legacy app's samples: a founder with a partner, two children and two people they know, a will with three bequests, the estate worked out by the shipped rules
// (DynastyRules.Settle), a retired founder, a debt for tax and two statements. The people are the cast the other pages' samples use. Names here are placeholders and never ship.
internal static partial class UiSamples
{
    public static readonly string[] DynastyStates =
    {
        "will", "will_pick", "will_dirty", "will_charity", "will_none", "estate", "estate_retire", "estate_handing", "estate_loan", "estate_under", "line", "line_elder", "off"
    };

    /// <summary>A recording stand-in for the director: each press says which action it was.</summary>
    public sealed class DynastyStub : IDynastyActions
    {
        public string Last;
        public int Count, Id, Steps;
        public KinKind Kind;
        public string Name;

        private void Did(string what, KinKind kind = KinKind.None, int id = 0, string name = null, int steps = 0)
        {
            Last = what;
            Count++;
            Kind = kind;
            Id = id;
            Name = name;
            Steps = steps;
        }

        public void Heir(KinKind kind, int id, string name) => Did("heir", kind, id, name);
        public void Share(KinKind kind, int id, string name, int steps) => Did("share", kind, id, name, steps);
        public void RetireAge(int steps) => Did("age", KinKind.None, 0, null, steps);
        public void Keep(int steps) => Did("keep", KinKind.None, 0, null, steps);
        public void Discard() => Did("discard");
        public void Sign() => Did("sign");
        public void Retire() => Did("retire");
    }

    public static List<KinPerson> DynastyPeople()
    {
        return new List<KinPerson>
        {
            new KinPerson { Kind = KinKind.Partner, Name = "Maya Okafor", Age = 58, Married = true, Closeness = 80 },
            new KinPerson { Kind = KinKind.Child, Name = "Tom", Age = 24, School = 2, FundMille = 1000, Closeness = 85 },
            new KinPerson { Kind = KinKind.Child, Name = "Hana", Age = 15, School = 1, FundMille = 400, Closeness = 60 },
            new KinPerson { Kind = KinKind.Person, Id = 4, Name = "Nadia Haddad", Age = 44, Family = true, Closeness = 55, Household = true, Room = 900000000 },
            new KinPerson { Kind = KinKind.Person, Id = 7, Name = "Tom Becker", Age = 61, Closeness = 40, Household = true, Room = 900000000 }
        };
    }

    public static Will DynastyWillOf(string state)
    {
        var w = new Will { RetireAge = 60, Keep = 2 };
        if (state == "will_none")
        {
            return w;
        }
        w.Written = true;
        w.Day = 310;
        w.HeirKind = KinKind.Child;
        w.HeirName = "Tom";
        w.Firm = "Hudson Legal";
        w.Fees = 1500;
        w.Bequests.Add(new Bequest { Kind = KinKind.Partner, Name = "Maya Okafor", Share = 200 });
        w.Bequests.Add(new Bequest { Kind = KinKind.Charity, Id = 0, Name = "Harbour Food Bank", Share = 100 });
        w.Bequests.Add(new Bequest { Kind = KinKind.Person, Id = 4, Name = "Nadia Haddad", Share = 50 });
        return w;
    }

    public static DynastyFacts DynastyFactsOf(string state)
    {
        var f = new DynastyFacts { Name = "Sam Rivera", Age = 62, Generation = 2, Level = 1000, Cash = 4200000, Spare = 4000000, Gifts = 18000, Fee = 300, Firm = "Hudson Legal" };
        if (state == "off")
        {
            f.Ready = false;
            return f;
        }
        f.People.AddRange(DynastyPeople());
        f.Signed = DynastyWillOf(state);
        f.Draft = f.Signed;
        if (state == "will_dirty")
        {
            f.Draft = f.Signed.Copy();
            DynastyWill.Move(f.Draft, KinKind.Person, 7, "Tom Becker", 1);
            f.Dirty = true;
        }
        if (state == "will_none")
        {
            f.Fee = 1200;
            f.Dirty = false;
        }
        f.DraftWhy = DynastyRules.Valid(f.Draft);
        var e = new EstateIn
        {
            Day = 400, Level = 1000, Mode = HandMode.Death, Generation = 2, From = f.Name, Cash = f.Cash, Reserved = f.Cash - f.Spare, Other = 9800000, Gifts = f.Gifts, Charities = true
        };
        if (state == "estate_under")
        {
            e.Other = -6500000;
        }
        if (state == "estate_loan")
        {
            e.Cash = 260000;
            e.Reserved = 0;
            f.Cash = f.Spare = 260000;
        }
        KinPerson heir = DynastyRules.Choose(f.Signed, f.People, out bool byLaw);
        f.HeirKnown = heir != null;
        f.Today = DynastyRules.Settle(e, f.Signed, heir, byLaw, f.People);
        e.Mode = HandMode.Retire;
        e.Keep = f.Signed.Keep;
        f.RetireToday = DynastyRules.Settle(e, f.Signed, heir, byLaw, f.People);
        f.Retire = DynastyRules.MayRetire(true, state == "estate_handing", f.Signed, f.Age, heir != null, false, state == "estate", state == "line_elder");
        if (state == "estate_handing")
        {
            f.Handing = true;
            f.Steps = 6;
            f.Step = 2;
        }
        if (state == "estate_loan")
        {
            f.LoanPrincipal = 900000;
            f.LoanOwed = 842300;
            f.LoanPerDay = 3450;
            f.LoanLate = 2;
        }
        f.Statements.Add(new Statement { Day = 120, Mode = HandMode.Retire, Generation = 1, From = "Alex Rivera", To = "Sam Rivera", Net = 6400000, Tax = 1470000, Paid = 320000 });
        f.Statements.Add(new Statement { Day = 260, Mode = HandMode.Elder, Generation = 1, From = "Alex Rivera", To = "Sam Rivera", Net = 410000, Tax = 164000 });
        f.Line.Add(new LineEntry { Name = "Alex Rivera", Generation = 1, Day = 120, Mode = HandMode.Retire, Net = 6400000, Tax = 1470000 });
        f.Kin.Add(new Kin { Kind = KinKind.Child, Name = "Jo Rivera", Held = 320000, Day = 120 });
        if (state == "line_elder")
        {
            f.Elder = new Elder { Name = "Alex Rivera", Born = -4800, Since = 120, Fund = 386000, Draw = 3900, Taxable = 5400000, Tax = 1470000 };
            f.ElderAge = 78;
        }
        f.RivalLines.Add("Generation 2 of the family of Space Karen leads the holding, 34 years old, since the last head retired on day 212. Estate tax paid: $41,800,000.");
        f.City = new CityEstates { Year = 6, Count = 41, Taxed = 2, Value = 3120000, Tax = 96000, Unpaid = 18400, LastCount = 55, LastTaxed = 1, LastValue = 3890000, LastTax = 22000 };
        return f;
    }

    public static VentModel Dynasty(UiWords t, string state, VentView view = null, IDynastyActions act = null)
    {
        VentWords w = VentWordsOf(t);
        DynastyFacts f = DynastyFactsOf(state);
        view = view ?? new VentView();
        switch (state)
        {
            case "will_pick": view.Tab = DynastyPages.TabWill; view.Pick = DynastyPages.PersonKey + 3; break;
            case "will_charity": view.Tab = DynastyPages.TabWill; view.Pick = DynastyPages.CharityKey + 1; break;
            case "estate":
            case "estate_retire":
            case "estate_handing":
            case "estate_loan":
            case "estate_under": view.Tab = DynastyPages.TabEstate; break;
            case "line":
            case "line_elder": view.Tab = DynastyPages.TabLine; break;
        }
        var m = new VentModel();
        DynastyPages.Fill(m, view, f, act ?? new DynastyStub(), w);
        return m;
    }
}
