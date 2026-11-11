using System;
using System.Collections.Generic;
using RealLifeOverhaul.Careers;
using RealLifeOverhaul.Dynasty;
using RealLifeOverhaul.Economy;
using RealLifeOverhaul.Fame;
using RealLifeOverhaul.Family;
using RealLifeOverhaul.Giving;
using RealLifeOverhaul.Markets;
using RealLifeOverhaul.Permadeath;
using RealLifeOverhaul.Population;
using RealLifeOverhaul.Rivals;
using RealLifeOverhaul.Tech;

namespace RealLifeOverhaul
{
    public static partial class Wiring
    {
        /// <summary>
        /// The dynasty (Dynasty/DynastyBridges) with the rest of the mod:
        ///
        ///   Reserved, Holdings    the investors' floor of the founder's firms (Tech); the Finance app's net worth, shares on the market counted, with what it leaves out (DynastyHoldings)
        ///   Wedding               the day the marriage began, as family life kept it
        ///   People, GrownChildren the partner and the children (Family) and the people the founder knows (the social life), with the household the population holds for each
        ///   Notary                the best law firm of the city, or the founder's own (Justice)
        ///   Law                   what the law has open against the founder and whether they are held now (DynastyGate says what that means)
        ///   Permadeath, Armed     Permadeath is on for the save: a death is its own
        ///   Charities, ToCharity  Giving's pots take a bequest
        ///   ToHousehold           a bequest lands in a household as the population holds it
        ///   Succeed               the heir becomes the head in Family, the social life, Careers, Standing and the record
        ///   Rival...              the tycoons' worth, the tax that leaves it, the new head's slow start and their names
        ///   GaveToPerson          Giving tells every gift to a person, for the estate's window
        ///   LifeRules.Ended       the population tells a household that ended with a death
        ///   DynastyLine           the tycoons' lines on the rich list and their panel
        ///   WillLine, EstateTax   the family side's line and the tax side's row
        ///   OtherDebts            the debt for estate tax among the Finance app's debts
        ///   News.Extra            the morning's City Ledger: the handovers
        ///
        /// Asked on a day's pass, a handover's step or a press, never per frame; a fault is said once (<see cref="Fault"/>) and the bridge's default answered.
        /// </summary>
        public static class DynastyLinks
        {
            private const int PeopleMost = 10;   // of the people the founder knows, the closest so many are offered for a bequest

            private static Action<int, List<NewsItem>> _paperBefore;
            private static Action<List<DebtLine>> _debtsBefore;
            private static readonly List<DynastyStory> Stories = new List<DynastyStory>(4);

            internal static void Connect()
            {
                DynastyBridges.Reserved = Reserved;
                DynastyBridges.Holdings = HoldingsNow;
                DynastyBridges.Wedding = () => FamilyApi.Active && FamilyState.Bond == Bond.Married ? Math.Max(0, FamilyState.S.MarriedDay) : -1;
                DynastyBridges.People = People;
                DynastyBridges.GrownChildren = GrownChildren;
                DynastyBridges.Notary = NotaryNow;
                DynastyBridges.Law = LawNow;
                DynastyBridges.Permadeath = () => Settings.Permadeath;
                DynastyBridges.Armed = () => PermadeathState.Armed;
                DynastyBridges.Charities = () => GivingDirector.Instance != null && GivingDirector.Instance.Running && GivingDirector.CauseWhy == 0;
                DynastyBridges.ToCharity = ToCharity;
                DynastyBridges.ToHousehold = ToHousehold;
                DynastyBridges.Succeed = Succeed;
                DynastyBridges.RivalWorth = RivalWorth;
                DynastyBridges.RivalTax = RivalTax;
                DynastyBridges.RivalSlow = RivalSlow;
                DynastyBridges.RivalName = index => index >= 0 && index < RivalCatalogue.TycoonCount ? RivalCatalogue.Tycoons[index].Name : string.Empty;
                GivingBridges.GaveToPerson = amount => DynastyDirector.Instance?.Gave(amount);
                Population.LifeRules.Ended = (savings, debt) => DynastyDirector.Instance?.HouseholdEnded(savings, debt);
                RivalBridges.DynastyLine = tycoon => DynastyDirector.Instance != null ? DynastyDirector.Instance.RivalLineOf(tycoon) : string.Empty;
                FamilyBridges.WillLine = () => DynastyDirector.Instance != null ? DynastyDirector.Instance.FamilyLine() : string.Empty;
                EconomyBridges.EstateTax = () => DynastyDirector.Instance != null ? DynastyDirector.Instance.TaxToday() : -1L;
                _debtsBefore = EconomyBridges.OtherDebts;
                EconomyBridges.OtherDebts = Debts;
                _paperBefore = News.Extra;
                News.Extra = Paper;
            }

            internal static void Disconnect()
            {
                DynastyBridges.Reset();
                GivingBridges.GaveToPerson = null;
                Population.LifeRules.Ended = null;
                RivalBridges.DynastyLine = null;
                FamilyBridges.WillLine = null;
                EconomyBridges.EstateTax = null;
                EconomyBridges.OtherDebts = _debtsBefore;
                _debtsBefore = null;
                News.Extra = _paperBefore;
                _paperBefore = null;
            }

            // ---- the estate

            private static long Reserved()
            {
                try
                {
                    TechDirector tech = TechDirector.Instance;
                    return tech != null ? GivingRules.Reserve(tech.FloorNow) : 0L;   // cents to whole money, rounded up
                }
                catch (Exception e)
                {
                    Fault("DynastyLinks.Reserved", e);
                    return 0L;
                }
            }

            private static readonly List<ICreditAccount> Credit = new List<ICreditAccount>(4);

            // What the estate is made of: the Finance app's net worth (the game's wealth less every debt the mod knows, with what is held on the stock market where that runs) and what
            // that figure leaves out. Each part is read from its own feature, in a guard of its own, so one that fails is said once and counts as nothing while the rest still count.
            // Assets are rounded down and debts up. Read on a handover's freeze, and for a page or the tax side once a day, a revision and a change of cash (never a frame).
            private static Holdings HoldingsNow()
            {
                var h = new Holdings();
                try
                {
                    MarketDirector market = MarketDirector.Instance;
                    h.Wealth = market != null && market.Running ? market.PlayerWorth() : Books.NetWorth();
                }
                catch (Exception e)
                {
                    Fault("DynastyLinks.Holdings (the net worth)", e);
                    h.Wealth = DynastyGame.Cash;
                }
                try
                {
                    CompanyState company = CompanyDirector.Instance != null ? CompanyDirector.Instance.State : null;
                    if (company != null)
                    {
                        h.TaxReserve = Math.Max(0, company.Tax.Reserve);   // set aside out of the cash for the year's tax bill; it is the founder's until it is paid
                        h.AuditOwed = Math.Max(0, company.Tax.Owed);       // back tax and fines an audit found
                    }
                }
                catch (Exception e)
                {
                    Fault("DynastyLinks.Holdings (the company)", e);
                }
                try
                {
                    OwnBank bank = OwnBankDirector.Instance != null ? OwnBankDirector.Instance.Bank : null;
                    if (bank != null && bank.Running && !bank.Test)
                    {
                        h.BankCapital = Math.Max(0L, OwnBankRules.CapitalNow(bank)) / 100L;   // the capital as the regulator counts it, retained earnings with it; cents to whole money, down
                    }
                }
                catch (Exception e)
                {
                    Fault("DynastyLinks.Holdings (the bank)", e);
                }
                try
                {
                    GivingState giving = GivingDirector.Instance != null ? GivingDirector.Instance.State : null;
                    for (int i = 0; giving != null && i < giving.Loans.Count; i++)
                    {
                        if (giving.Loans[i].State == LoanState.Open)
                        {
                            h.LoansOut += Math.Max(0, giving.Loans[i].Remaining);
                        }
                    }
                }
                catch (Exception e)
                {
                    Fault("DynastyLinks.Holdings (loans out)", e);
                }
                try
                {
                    for (int i = 0; SocialState.Loaded && i < SocialState.Count; i++)
                    {
                        SocialPerson p = SocialState.People[i];
                        if (p != null)
                        {
                            h.FriendsOwe += Math.Max(0, p.Loan);     // what they owe the player
                            h.FriendsOwed += Math.Max(0, p.Debt);    // what the player owes them
                        }
                    }
                }
                catch (Exception e)
                {
                    Fault("DynastyLinks.Holdings (the people)", e);
                }
                try
                {
                    // The credit accounts of other features that the Finance app's net worth does not take off: the student loan, the construction loans.
                    Credit.Clear();
                    EconomyBridges.OtherAccounts?.Invoke(Credit);
                    for (int i = 0; i < Credit.Count; i++)
                    {
                        h.CreditLoans += (long)Math.Ceiling(Math.Max(0f, Credit[i].Balance));
                    }
                    Credit.Clear();
                }
                catch (Exception e)
                {
                    Fault("DynastyLinks.Holdings (the loans)", e);
                }
                try
                {
                    GameInstance save = SaveGameManager.Current;
                    if (save != null)
                    {
                        if (save.currentUnpaidTaxes != null)
                        {
                            h.UnpaidTaxes = (long)Math.Ceiling(Math.Max(0f, save.currentUnpaidTaxes.totalToPay) * (save.currentUnpaidTaxes.lateFeeApplied ? 1.1 : 1.0));   // as the game asks it
                        }
                        h.BackTaxes = (long)Math.Ceiling(Math.Max(0f, save.currentBackTaxes));
                    }
                }
                catch (Exception e)
                {
                    Fault("DynastyLinks.Holdings (the game's taxes)", e);
                }
                return h;
            }

            // The debt for estate tax among the debts the Finance app counts, so the net worth (and the next estate) has it taken off.
            private static void Debts(List<DebtLine> into)
            {
                try
                {
                    _debtsBefore?.Invoke(into);
                }
                catch (Exception e)
                {
                    Fault("DynastyLinks.Debts (the debts before)", e);
                }
                try
                {
                    DynastyDirector d = DynastyDirector.Instance;
                    EstateLoan l = d != null ? d.LoanOwed : null;   // shown whatever the option says: the debt is owed all the same
                    if (l != null && l.Open)
                    {
                        into.Add(new DebtLine
                        {
                            NameKey = "reallife_dynasty_debt", Owed = (int)Math.Min(int.MaxValue / 2, (l.Owed + 99) / 100), PerDay = (int)Math.Min(int.MaxValue / 2, (l.PerDay + 99) / 100),
                            LateDays = l.Late
                        });
                    }
                }
                catch (Exception e)
                {
                    Fault("DynastyLinks.Debts", e);
                }
            }

            // ---- the people

            private static bool PeopleOn => Settings.Social && SocialState.Loaded;

            // The household the population holds for one of the social cast, where money can reach it: its place, and the most it can take.
            private static bool HouseholdOf(int index, out PopulationState pop, out int h)
            {
                pop = GivingGame.People();   // null in a session, with the living population off, or while it follows another player's
                h = -1;
                if (pop == null)
                {
                    return false;
                }
                int p = GivingPeople.ResidentOf(pop, index);
                h = p >= 0 ? GivingPeople.HouseholdOf(pop, p) : -1;
                return h >= 0 && GivingPeople.Helpable(pop, h);
            }

            private static void Housed(KinPerson c, int index)
            {
                if (HouseholdOf(index, out PopulationState pop, out int h))
                {
                    c.Household = true;
                    c.Room = Math.Max(0L, (long)int.MaxValue / 2 - pop.Households.Savings[h] + pop.Households.Debt[h]);
                }
            }

            private static void People(int founderAge, int day, List<KinPerson> into)
            {
                into.Clear();
                try
                {
                    int year = DynastyGame.DaysPerYear;
                    SocialPerson partner = PeopleOn ? SocialState.Partner : null;
                    if (FamilyApi.Active && FamilyState.Together)
                    {
                        FamilySave s = FamilyState.S;
                        bool same = partner != null && partner.Met && partner.Name == Partner.Name;
                        DynastyDirector wed = DynastyDirector.Instance;
                        bool married = FamilyState.Bond == Bond.Married;
                        var c = new KinPerson
                        {
                            Kind = KinKind.Partner, Name = Partner.Name ?? string.Empty, Married = married, Closeness = (int)Math.Round(s.PAffection),
                            Relief = married && DynastyRules.WedLongEnough(wed != null ? wed.WedDay : 0, day, year),   // only a marriage of the window's length frees a part
                            Age = DynastyRules.PartnerAge(same ? SocialRules.AgeOn(partner.Age, partner.AgeDay, partner.Birthday, day, year) : 0, founderAge)
                        };
                        if (same)
                        {
                            c.Id = partner.Index;
                            Housed(c, partner.Index);
                        }
                        if (c.Name.Length > 0)
                        {
                            into.Add(c);
                        }
                    }
                    List<ChildRecord> kids = FamilyApi.Active ? FamilyState.S.Children : null;
                    DynastyDirector d = DynastyDirector.Instance;
                    for (int i = 0; kids != null && i < kids.Count; i++)
                    {
                        ChildRecord k = kids[i];
                        if (k == null || string.IsNullOrEmpty(k.Name))
                        {
                            continue;
                        }
                        into.Add(new KinPerson
                        {
                            Kind = KinKind.Child, Name = k.Name, School = k.School, FundMille = (int)Math.Min(1000L, (long)Math.Max(0, k.Fund) * 1000 / FamilyBalance.FundFull),
                            Closeness = (int)Math.Round(k.Closeness), Age = DynastyRules.ChildAge(k.Age, k.Grown, d != null ? d.GrownDay(k.Name) : day, day, year)
                        });
                    }
                    int first = into.Count;
                    for (int i = 0; PeopleOn && i < SocialState.Count; i++)
                    {
                        SocialPerson p = SocialState.People[i];
                        if (p == null || !p.Met || p == partner || string.IsNullOrEmpty(p.Name))
                        {
                            continue;
                        }
                        var c = new KinPerson
                        {
                            Kind = KinKind.Person, Id = p.Index, Name = p.Name, Family = p.IsFamily, Closeness = p.Closeness,
                            Age = SocialRules.AgeOn(p.Age, p.AgeDay, p.Birthday, day, year)
                        };
                        Housed(c, p.Index);
                        // kept in order as they are added: the family first, then the closest; the same order on every machine (the cast's own index breaks a tie)
                        int at = first;
                        while (at < into.Count && (into[at].Family != c.Family ? into[at].Family : into[at].Closeness >= c.Closeness))
                        {
                            at++;
                        }
                        into.Insert(at, c);
                        if (into.Count > first + PeopleMost)
                        {
                            into.RemoveAt(into.Count - 1);
                        }
                    }
                }
                catch (Exception e)
                {
                    Fault("DynastyLinks.People", e);
                }
            }

            private static void GrownChildren(List<string> into)
            {
                try
                {
                    List<ChildRecord> kids = FamilyApi.Active ? FamilyState.S.Children : null;
                    for (int i = 0; kids != null && i < kids.Count; i++)
                    {
                        if (kids[i] != null && kids[i].Grown && !string.IsNullOrEmpty(kids[i].Name))
                        {
                            into.Add(kids[i].Name);
                        }
                    }
                }
                catch (Exception e)
                {
                    Fault("DynastyLinks.GrownChildren", e);
                }
            }

            // ---- the law

            private static Notary NotaryNow()
            {
                try
                {
                    List<LawFirm> firms = Justice.Active ? LegalCounsel.List() : null;
                    for (int i = 0; firms != null && i < firms.Count; i++)
                    {
                        if (firms[i].Own)
                        {
                            return new Notary { Name = firms[i].Name, Own = true };   // the founder's own firm draws it up for nothing
                        }
                    }
                    return firms != null && firms.Count > 0 ? new Notary { Name = firms[0].Name } : default(Notary);
                }
                catch (Exception e)
                {
                    Fault("DynastyLinks.Notary", e);
                    return default(Notary);
                }
            }

            // What the law has open against the founder, and whether they are held or in the middle of something staged: DynastyGate says what it means (retiring is refused while
            // anything is open, so it is no way out of any of it; the estate does not pass in a cell, in prison, before the court or during a scene). A state that cannot be read is
            // open and held.
            private static LawFacts LawNow()
            {
                var f = new LawFacts();
                try
                {
                    int day = TimeHelper.CurrentDay;
                    if (JusticeState.Loaded)
                    {
                        f.OpenCount = JusticeState.OpenCount;
                        f.HasWarrant = JusticeState.HasWarrant;
                        f.ServeWarrant = JusticeState.ServeWarrant;
                        f.JailPending = JusticeState.JailHoursPending > 0;
                        f.Probation = JusticeState.OnProbation(day);
                        f.CommunityHours = JusticeState.CommunityHours;
                        f.Suspended = JusticeState.Suspended(day);
                    }
                    f.InPrison = JusticeState.InPrison;
                    f.HearNow = JusticeState.HearNow != 0;
                    f.DrivingSuspended = Licences.Driving.Suspended;
                    f.WantedCharges = Law.LawState.WantedCharges;
                    f.Staged = EconomyBridges.Held != null && EconomyBridges.Held();
                    f.Read = true;
                }
                catch (Exception e)
                {
                    Fault("DynastyLinks.Law", e);
                    f.Read = false;
                    f.Staged = true;
                }
                return f;
            }

            // ---- where a bequest lands

            private static long ToCharity(int charity, long amount)
            {
                try
                {
                    GivingDirector g = GivingDirector.Instance;
                    return g != null ? g.Bequest((Charity)Math.Max(0, Math.Min((int)Charity.Count - 1, charity)), amount) : 0L;
                }
                catch (Exception e)
                {
                    Fault("DynastyLinks.ToCharity", e);
                    return 0L;
                }
            }

            private static long ToHousehold(int index, long amount)
            {
                try
                {
                    if (amount <= 0 || !HouseholdOf(index, out PopulationState pop, out int h))
                    {
                        return 0L;
                    }
                    long room = Math.Max(0L, (long)int.MaxValue / 2 - pop.Households.Savings[h] + pop.Households.Debt[h]);
                    long lands = Math.Min(amount, room);
                    GivingPeople.Help(pop, h, lands, out long _, out long _);
                    return lands;
                }
                catch (Exception e)
                {
                    Fault("DynastyLinks.ToHousehold", e);
                    return 0L;
                }
            }

            // ---- the heir becomes the head

            // Called while the played person is still the founder by name. Each feature is told in turn and a fault in one does not stop the others.
            private static void Succeed(Statement s)
            {
                int day = TimeHelper.CurrentDay;
                SocialPerson partner = null, heir = null;
                try
                {
                    partner = PeopleOn ? SocialState.Partner : null;
                    heir = s.HeirKind == KinKind.Partner ? partner : s.HeirKind == KinKind.Person && PeopleOn ? SocialState.Get(s.HeirId) : null;
                    if (heir != null && heir.Name != s.To)
                    {
                        heir = null;   // the cast has changed since the estate was frozen: nobody else is taken out of it
                    }
                }
                catch (Exception e)
                {
                    Fault("DynastyLinks.Succeed (the people)", e);
                }
                try
                {
                    if (partner != null && partner != heir)
                    {
                        // The founder's partner is a parent or a relative of the new head: known as before, with no romance between them.
                        partner.Stage = SocialStage.CloseFriend;
                        partner.StageDay = day;
                        partner.MovedIn = false;
                        partner.Dates = 0;
                        partner.Compatible = false;
                        if (SocialState.Plan.Active && SocialState.Plan.Person == partner.Index)
                        {
                            SocialState.Plan.Clear();
                        }
                    }
                    if (heir != null)
                    {
                        // The one who is played now is no longer somebody to call: they leave the cast by the social life's own way, and nobody owes themselves.
                        heir.Debt = heir.Loan = 0;
                        heir.MovedIn = false;
                        GivingDirector.Instance?.Merged(heir.Index);
                        SocialDirector.LoseTouch(heir, heir.Origin, "they carry on as the head of the family");
                    }
                }
                catch (Exception e)
                {
                    Fault("DynastyLinks.Succeed (the social life)", e);
                }
                // The family after the people: it reads who the partner is from the social life, which has just been told.
                try
                {
                    FamilyDirector.Instance?.NewHead(s.HeirKind == KinKind.Partner, s.From, s.HeirKind == KinKind.Child ? s.To : null);
                }
                catch (Exception e)
                {
                    Fault("DynastyLinks.Succeed (the family)", e);
                }
                try
                {
                    CareersDirector.Instance?.HandOver(s.To, s.Upbringing, day);
                }
                catch (Exception e)
                {
                    Fault("DynastyLinks.Succeed (the education)", e);
                }
                try
                {
                    // The family name keeps a part of how well the city knew the founder; integrity was their own and goes half the way back to where everybody starts.
                    // What the customers, the workers and the neighbourhoods think follows the businesses, which stay.
                    FameFile fame = FameDirector.Instance != null ? FameDirector.Instance.File : null;
                    if (fame != null)
                    {
                        fame.Recognition = DynastyRules.FameKept(fame.Recognition);
                        fame.Press = DynastyRules.FameKept(fame.Press);
                        fame.Rep[(int)FamePart.Integrity] = DynastyRules.OwnAfter(fame.Rep[(int)FamePart.Integrity], FameRules.Start);
                    }
                }
                catch (Exception e)
                {
                    Fault("DynastyLinks.Succeed (the standing)", e);
                }
                // The record is not touched: the convictions, the police's lines and everything open (a case, a fine, a plan to pay, probation, a suspension, a warrant) stay exactly as
                // they are and are answered by whoever leads the family. Justice seals a record by its own clean days, and the credit file reads the police's lines by their own
                // days, so a handover neither wipes a past nor improves a rating.
            }

            // ---- the rivals

            // A tycoon's worth while they are forward and the rivals are not resting (a Going Public session); -1 otherwise.
            private static long RivalWorth(int index)
            {
                try
                {
                    RivalFile f = RivalDirector.Instance != null && !GoingPublic.InSession ? RivalDirector.Instance.File : null;
                    Tycoon t = f != null && index >= 0 && index < f.Tycoons.Length ? f.Tycoons[index] : null;
                    return t != null && t.Active ? t.Worth : -1L;
                }
                catch (Exception e)
                {
                    Fault("DynastyLinks.RivalWorth", e);
                    return -1L;
                }
            }

            // The tax leaves a tycoon's worth, but only the worth over their floor (the start's worth at today's prices, which the rivals raise a worth to every day):
            // a tax taken from the floor itself would be made up the next morning. Returns what really left them.
            private static long RivalTax(int index, long tax)
            {
                try
                {
                    RivalFile f = RivalDirector.Instance != null ? RivalDirector.Instance.File : null;
                    Tycoon t = f != null && index >= 0 && index < f.Tycoons.Length ? f.Tycoons[index] : null;
                    if (t == null || !t.Active)
                    {
                        return 0L;
                    }
                    long paid = DynastyRivals.Paid(tax, t.Worth, RivalRules.At(t.Def.StartWorth, Prices.Level));
                    t.Worth -= paid;
                    return paid;
                }
                catch (Exception e)
                {
                    Fault("DynastyLinks.RivalTax", e);
                    return 0L;
                }
            }

            // A new head finds their feet: the rivals' own slow period (their fortune grows at half the pace), never shortened.
            private static void RivalSlow(int index, int until)
            {
                try
                {
                    RivalFile f = RivalDirector.Instance != null ? RivalDirector.Instance.File : null;
                    Tycoon t = f != null && index >= 0 && index < f.Tycoons.Length ? f.Tycoons[index] : null;
                    if (t != null)
                    {
                        t.SlowUntil = Math.Max(t.SlowUntil, until);
                    }
                }
                catch (Exception e)
                {
                    Fault("DynastyLinks.RivalSlow", e);
                }
            }

            // Yesterday's handovers in the morning's City Ledger: a very large estate leads the front page, the rest follow the others' pieces.
            private static void Paper(int day, List<NewsItem> into)
            {
                try
                {
                    _paperBefore?.Invoke(day, into);
                }
                catch (Exception e)
                {
                    Fault("DynastyLinks.Paper (the pieces before)", e);
                }
                try
                {
                    DynastyDirector director = DynastyDirector.Instance;
                    if (director == null)
                    {
                        return;
                    }
                    director.Stories(day, Stories);
                    bool led = false;
                    for (int i = 0; i < Stories.Count; i++)
                    {
                        var item = new NewsItem { Key = Stories[i].Key, Data = Stories[i].Data };
                        if (Stories[i].Lead && !led && into.Count > 0)
                        {
                            into.Insert(1, item);   // after the masthead
                            led = true;
                        }
                        else
                        {
                            into.Add(item);
                        }
                    }
                    Stories.Clear();
                }
                catch (Exception e)
                {
                    Fault("DynastyLinks.Paper", e);
                }
            }
        }
    }
}
