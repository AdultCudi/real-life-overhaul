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
        ///   Reserved, NetWorth    the investors' floor of the founder's firms (Tech) and the Finance app's net worth, shares on the market counted
        ///   People, GrownChildren the partner and the children (Family) and the people the founder knows (the social life), with the household the population holds for each
        ///   Notary                the best law firm of the city, or the founder's own (Justice)
        ///   LawOpen, Custody      whether the law still wants the founder, and whether they are held now
        ///   Final                 Permadeath is on for the save: a death is its own
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
                DynastyBridges.NetWorth = NetWorth;
                DynastyBridges.People = People;
                DynastyBridges.GrownChildren = GrownChildren;
                DynastyBridges.Notary = NotaryNow;
                DynastyBridges.LawOpen = LawOpen;
                DynastyBridges.Custody = Custody;
                DynastyBridges.Final = () => Settings.Permadeath && PermadeathState.Armed;
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

            // The Finance app's net worth (the game's wealth less every debt the mod knows) with what is held on the stock market, where that runs.
            private static long NetWorth()
            {
                try
                {
                    MarketDirector market = MarketDirector.Instance;
                    return market != null && market.Running ? market.PlayerWorth() : Books.NetWorth();
                }
                catch (Exception e)
                {
                    Fault("DynastyLinks.NetWorth", e);
                    return Books.NetWorth();
                }
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
                    EstateLoan l = d != null && d.Running ? d.State.Loan : null;
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
                        var c = new KinPerson
                        {
                            Kind = KinKind.Partner, Name = Partner.Name ?? string.Empty, Married = FamilyState.Bond == Bond.Married, Closeness = (int)Math.Round(s.PAffection),
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

            // Anything the law still has open against the founder: retiring is refused while it is, so it is no way out of any of it.
            private static bool LawOpen()
            {
                try
                {
                    int day = TimeHelper.CurrentDay;
                    bool courts = JusticeState.Loaded && (JusticeState.OpenCount > 0 || JusticeState.HasWarrant || JusticeState.ServeWarrant || JusticeState.InPrison
                                                         || JusticeState.JailHoursPending > 0 || JusticeState.OnProbation(day) || JusticeState.CommunityHours > 0
                                                         || JusticeState.Suspended(day));
                    return courts || Licences.Driving.Suspended || Law.LawState.WantedCharges != 0;
                }
                catch (Exception e)
                {
                    Fault("DynastyLinks.LawOpen", e);
                    return true;   // not known: nobody retires past it
                }
            }

            // Held now, or in the middle of something staged: the estate does not pass in a cell, in prison, before the court or during a scene.
            private static bool Custody()
            {
                try
                {
                    return JusticeState.InPrison || JusticeState.HearNow != 0 || (EconomyBridges.Held != null && EconomyBridges.Held());
                }
                catch (Exception e)
                {
                    Fault("DynastyLinks.Custody", e);
                    return true;
                }
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
                    FamilyDirector.Instance?.NewHead(s.HeirKind == KinKind.Partner, s.From);
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
                try
                {
                    // A record is a person's own: the founder's convictions are sealed and the police's lines closed. Nothing open is touched: a case, a fine, a plan to pay,
                    // probation and a suspension stay exactly as they are and are answered by whoever leads the family.
                    if (JusticeState.Loaded)
                    {
                        JusticeState.Seal();
                    }
                    if (!JusticeState.InPrison && Law.LawState.Loaded)
                    {
                        Law.Record.Clear();
                    }
                }
                catch (Exception e)
                {
                    Fault("DynastyLinks.Succeed (the record)", e);
                }
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
