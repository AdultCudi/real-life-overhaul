using System;
using System.Collections.Generic;
using RealLifeOverhaul.BuildTrades;
using RealLifeOverhaul.Construction;
using RealLifeOverhaul.Economy;
using RealLifeOverhaul.Events;
using RealLifeOverhaul.Fame;
using RealLifeOverhaul.Flare;
using RealLifeOverhaul.Landmarks;
using RealLifeOverhaul.NewBuildings;
using RealLifeOverhaul.Uptown;
using UnityEngine;

namespace RealLifeOverhaul
{
    public static partial class Wiring
    {
        /// <summary>
        /// The construction game (Construction/ConstructionBridges) with the rest of the mod, and the city's construction
        /// programme (Civic/SiteBuilders) with the builders and the copy engine:
        ///
        ///   SiteBuilders.*            a builder's site's pace, owner, name, design, lot and handover; a city site to tender;
        ///                             the finished buildings' looks (NewBuildings/BuildingCopies); City Hall's sixth topic
        ///                             and a builder's notice board (Construction/PlanningCall)
        ///   Designs, Footprint        the copy engine's designs that fit some ground, and a building's footprint
        ///   StartPlot ... Permits     the city's programme (Civic/CivicWorks): sites started, read, handed over, dropped
        ///   BusinessRate ... Credit   the banks' business rate and the credit file (Economy)
        ///   MaterialFactor            the raw materials exchange (Markets)
        ///   Record ... Storm          the record and the courts (Justice), Flare, Standing (Fame), the weather (Events)
        ///   Qualified ... DesignOf    the building trades (BuildTrades/TradesBridges): the player's own qualification and design
        ///   News.Extra                the morning's City Ledger
        ///   CodeAhead ... Followers   the city around the company: the building code's change and the next election, the
        ///                             one council's planning sitting and vote (Civic), the courts for a debt either way and a
        ///                             ring found out (Justice), the player's following (Flare)
        ///   ZoningBridges.*           what the council's planning business asks of the builders (Civic/ZoningBridges)
        ///
        ///   Lots ... Finish           the new district (Uptown): its lots, their traffic and sizes, its land's price, and a
        ///                             finished building handed to it (UptownLots, DistrictBuildings)
        ///
        /// Asked on an hour, a day, a page or a press, never per frame; a fault is said once (<see cref="Fault"/>) and the
        /// bridge's default answered.
        /// </summary>
        public static class ConstructionLinks
        {
            private const string Owner = "construction";

            private static Action<int, List<NewsItem>> _paperBefore;
            private static Action<List<ICreditAccount>> _accountsBefore;
            private static readonly List<BuildStory> Stories = new List<BuildStory>(8);
            private static readonly List<DesignFit> Fits = new List<DesignFit>(32);
            private static readonly ConstructionLoan Loan = new ConstructionLoan();

            private static ConstructionDirector Director => ConstructionDirector.Instance;

            internal static void Connect()
            {
                SiteBuilders.Hours = Hours;
                SiteBuilders.Owns = site => Director != null && Director.Owns(site.Id);
                SiteBuilders.Shown = site => Director == null || Director.Shown(site.Id);
                SiteBuilders.Name = site => Director != null ? Director.NoticeName(site.Id) : null;
                SiteBuilders.Handed = site => Director?.SiteHanded(site.Id);
                SiteBuilders.Started = site => Director?.SiteStarted(Facts(site));
                SiteBuilders.Design = site => Director != null ? Director.DesignFor(site.Id) : null;
                SiteBuilders.Tag = site => Director != null ? Director.TagFor(site.Id) : null;
                SiteBuilders.Taken = ConstructionDirector.LotBuilt;
                SiteBuilders.CopyOver = (building, design) => BuildingCopies.PlaceOver(building, design, null, Owner, true);
                SiteBuilders.CopyOnPlot = (design, middle, yaw, width, depth) => BuildingCopies.PlaceOnPlot(design, null, middle, yaw, width, depth, Owner);
                SiteBuilders.CopyRemove = handle => BuildingCopies.RemoveOutOfView(handle as CopyHandle);
                SiteBuilders.CopyGone = handle => !(handle is CopyHandle copy) || copy.Gone;
                SiteBuilders.PlanningTopic = PlanningCall.Topic;
                SiteBuilders.Notice = PlanningCall.Notice;

                ConstructionBridges.Lots = UptownLotsInto;
                ConstructionBridges.LotsStamp = () => UptownLots.Stamp * 397 + DistrictBuildings.Stamp;
                ConstructionBridges.DistrictLives = () => DistrictBuildings.Lives;
                ConstructionBridges.LotTaken = LotTaken;
                ConstructionBridges.LotBuilt = LotBuilt;
                ConstructionBridges.SizeTaken = SizeTaken;
                ConstructionBridges.LandPrice = () => DistrictBuildings.LandPricePerSqm;
                ConstructionBridges.Finish = Finish;
                ConstructionBridges.Designs = Designs;
                ConstructionBridges.Footprint = Footprint;
                ConstructionBridges.StartPlot = StartPlot;
                ConstructionBridges.StartWorks = StartWorks;
                ConstructionBridges.Site = Site;
                ConstructionBridges.SetContractor = SetContractor;
                ConstructionBridges.FinishSite = id => Works() is CivicWorks w && w.Finish(w.Find(id), TimeHelper.CurrentDay);
                ConstructionBridges.DropSite = id => Works() is CivicWorks w && w.Drop(w.Find(id));
                ConstructionBridges.SiteCount = () => Works() is CivicWorks w ? w.State.Sites.Count : 0;
                ConstructionBridges.AdvanceSite = id => Works() is CivicWorks w && w.Find(id) is SiteState s && w.AdvanceStage(s, TimeHelper.CurrentDay);
                ConstructionBridges.Ground = Ground;
                ConstructionBridges.Permits = () => Works() is CivicWorks w ? Mathf.Clamp(w.Permits, -1, 1) : 0;
                ConstructionBridges.BusinessRate = BusinessRate;
                ConstructionBridges.BankName = () => BankTerms.House != null ? BankTerms.House.Name : null;
                ConstructionBridges.Credit = Credit;
                ConstructionBridges.MaterialFactor = item => Markets.MarketDirector.Instance != null ? Markets.MarketDirector.Instance.MaterialFactor(item) : double.NaN;
                ConstructionBridges.Record = () => Standing.Convictions > 0;
                ConstructionBridges.Lawyer = () => JusticeState.Loaded && JusticeState.Lawyer == LawyerKind.Firm;
                ConstructionBridges.Claim = BuildClaims.File;
                ConstructionBridges.Bribery = TaxCases.FileBribery;
                ConstructionBridges.Opened = (place, name) => FlareDirector.Instance?.Happened(FlareSource.Opened, -1, place, name, 0);
                ConstructionBridges.Standing = (points, scandal) => FameDirector.Instance?.Moved(scandal ? FamePart.Integrity
                    : FamePart.Neighbourhood, scandal ? FameCause.Scandal : FameCause.Press, points);
                ConstructionBridges.Storm = () => Weather.Sky == Sky.Storm;
                ConstructionBridges.Qualified = TradesBridges.Qualified;
                ConstructionBridges.Commission = (project, storeys, units, budget) => TradesBridges.Commission(project, storeys, units, (int)Math.Min(int.MaxValue, Math.Max(0L, budget)));
                ConstructionBridges.DesignOf = DesignOf;
                ConstructionBridges.LandmarkSites = LandmarkLinks.Sites;   // Uptown's landmarks (Scripts/Landmarks): their sites, tenders and works
                ConstructionBridges.LandmarkHeld = LandmarksDirector.Held;
                ConstructionBridges.LandmarkName = LandmarksDirector.NameOf;
                ConstructionBridges.LandmarkWords = LandmarksDirector.Words;
                ConstructionBridges.LandmarkDesign = LandmarkLinks.Design;
                ConstructionBridges.LandmarkAwarded = LandmarksDirector.Awarded;
                ConstructionBridges.LandmarkHanded = LandmarksDirector.Handed;
                ConstructionBridges.LandmarkWorks = LandmarksDirector.Works;
                ConstructionBridges.LandmarkPace = LandmarksDirector.Pace;
                ConstructionBridges.LandmarkBuilt = LandmarksDirector.Built;
                ConstructionBridges.CodeAhead = CodeAhead;
                ConstructionBridges.CouncilDay = CouncilDay;
                ConstructionBridges.Council = Council;
                ConstructionBridges.Sue = OwnClaims.File;
                ConstructionBridges.SuitResult = OwnClaims.Result;
                ConstructionBridges.Unpaid = BuildClaims.FileUnpaid;
                ConstructionBridges.BidRigging = TaxCases.FileBidRigging;
                ConstructionBridges.Withdraw = (kind, number) => kind == 0 ? TaxCases.Withdraw(number) : kind == 1 ? BuildClaims.Withdraw(number) : OwnClaims.Withdraw(number);
                ConstructionBridges.Followers = Followers;
                ConstructionBridges.CourtOpen = () => DebtClaims.Available;
                ConstructionBridges.HearClaim = number => OwnClaims.HearNow(number, TimeHelper.CurrentDay);
                ZoningBridges.Decided = (lot, use, carried, ayes, noes) => Director?.ZoneDecided(lot, use, carried, ayes, noes);
                ZoningBridges.Rezoned = (lotClass, use, add) => Director?.ClassRezoned(lotClass, use, add);
                ZoningBridges.Interest = () => Director != null && Director.Loaded && Director.Interest;
                ZoningBridges.LotWords = lot => Director != null && Director.Loaded ? Director.LotWords(lot) : null;
                ZoningBridges.ClassZones = lotClass => Director != null && Director.Loaded ? Director.ClassZones(lotClass) : LandRules.ZonesOf(lotClass);
                ZoningBridges.FirstZonesOf = LandRules.ZonesOf;
                ZoningBridges.Suit = (lotClass, use) => BuildCatalogue.ZoneOdds[Mathf.Clamp(use, 0, (int)BuildUse.Count - 1)][Mathf.Clamp(lotClass, 0, BuildCatalogue.ClassCount - 1)];
                ZoningBridges.District = ConstructionDirector.DistrictLives;
                _paperBefore = News.Extra;
                News.Extra = Paper;
                _accountsBefore = EconomyBridges.OtherAccounts;
                EconomyBridges.OtherAccounts = Accounts;   // again at each start of the credit side, whose ledger forgets every account
                CreditLedger.Register(Loan);
            }

            internal static void Disconnect()
            {
                SiteBuilders.Reset();
                ConstructionBridges.Lots = null;
                ConstructionBridges.LotsStamp = null;
                ConstructionBridges.DistrictLives = null;
                ConstructionBridges.LotTaken = null;
                ConstructionBridges.LotBuilt = null;
                ConstructionBridges.SizeTaken = null;
                ConstructionBridges.LandPrice = null;
                ConstructionBridges.Finish = null;
                ConstructionBridges.Designs = null;
                ConstructionBridges.Footprint = null;
                ConstructionBridges.StartPlot = null;
                ConstructionBridges.StartWorks = null;
                ConstructionBridges.Site = null;
                ConstructionBridges.SetContractor = null;
                ConstructionBridges.FinishSite = null;
                ConstructionBridges.DropSite = null;
                ConstructionBridges.SiteCount = null;
                ConstructionBridges.AdvanceSite = null;
                ConstructionBridges.Ground = null;
                ConstructionBridges.Permits = null;
                ConstructionBridges.BusinessRate = null;
                ConstructionBridges.BankName = null;
                ConstructionBridges.Credit = null;
                ConstructionBridges.MaterialFactor = null;
                ConstructionBridges.Record = null;
                ConstructionBridges.Lawyer = null;
                ConstructionBridges.Claim = null;
                ConstructionBridges.Bribery = null;
                ConstructionBridges.Opened = null;
                ConstructionBridges.Standing = null;
                ConstructionBridges.Storm = null;
                ConstructionBridges.Qualified = null;
                ConstructionBridges.Commission = null;
                ConstructionBridges.DesignOf = null;
                ConstructionBridges.LandmarkSites = null;
                ConstructionBridges.LandmarkHeld = null;
                ConstructionBridges.LandmarkName = null;
                ConstructionBridges.LandmarkWords = null;
                ConstructionBridges.LandmarkDesign = null;
                ConstructionBridges.LandmarkAwarded = null;
                ConstructionBridges.LandmarkHanded = null;
                ConstructionBridges.LandmarkWorks = null;
                ConstructionBridges.LandmarkPace = null;
                ConstructionBridges.LandmarkBuilt = null;
                ConstructionBridges.CodeAhead = null;
                ConstructionBridges.CouncilDay = null;
                ConstructionBridges.Council = null;
                ConstructionBridges.Sue = null;
                ConstructionBridges.SuitResult = null;
                ConstructionBridges.Unpaid = null;
                ConstructionBridges.BidRigging = null;
                ConstructionBridges.Withdraw = null;
                ConstructionBridges.Followers = null;
                ConstructionBridges.CourtOpen = null;
                ConstructionBridges.HearClaim = null;
                ZoningBridges.Reset();
                News.Extra = _paperBefore;
                _paperBefore = null;
                EconomyBridges.OtherAccounts = _accountsBefore;
                _accountsBefore = null;
                CreditLedger.Unregister(Loan);
            }

            /// <summary>True when every bridge the construction game asks of the trades is filled in (the wiring's self-check).</summary>
            public static bool TradesWired => ConstructionBridges.Qualified != null && ConstructionBridges.Commission != null && ConstructionBridges.DesignOf != null;

            private static CivicWorks Works()
            {
                CivicDirector civic = CivicDirector.Instance;
                return civic != null ? civic.Works : null;
            }

            // ------------------------------------------------------------------ the programme asks the builders

            private static float Hours(SiteState site, float hours)
            {
                try
                {
                    ConstructionDirector d = Director;
                    return d != null && site != null ? d.WorkOn(site.Id, site.Done, site.Needed, hours) : hours;
                }
                catch (Exception e)
                {
                    Fault("ConstructionLinks.Hours", e);
                    return hours;
                }
            }

            private static SiteFacts Facts(SiteState site)
            {
                return site == null ? default(SiteFacts) : new SiteFacts
                {
                    Id = site.Id, Kind = (int)site.Kind, Street = site.Street, Number = site.Number, Hours = site.Needed, Done = site.Done, At = new Vector3(site.X, site.Y, site.Z),
                    Contractor = site.Contractor, Known = true
                };
            }

            // ------------------------------------------------------------------ the builders ask the programme

            private static SiteFacts Site(int id)
            {
                try
                {
                    CivicWorks works = Works();
                    return Facts(works != null ? works.Find(id) : null);
                }
                catch (Exception e)
                {
                    Fault("ConstructionLinks.Site", e);
                    return default(SiteFacts);
                }
            }

            private static int StartPlot(Vector3 middle, int yaw, int width, int depth, int storeys, int contractor, int hours, out string why)
            {
                why = null;
                try
                {
                    CivicWorks works = Works();
                    if (works == null || !Settings.CivicConstruction)
                    {
                        why = "the city's construction sites are off";
                        return 0;
                    }
                    SiteState site = works.StartPlot(middle, yaw, width, depth, storeys, TimeHelper.CurrentDay, out why);
                    if (site == null)
                    {
                        return 0;
                    }
                    if (hours > 0)
                    {
                        site.Needed = hours;
                    }
                    site.Contractor = Mathf.Clamp(contractor, 0, CivicCatalogue.Contractors.Length - 1);
                    return site.Id;
                }
                catch (Exception e)
                {
                    Fault("ConstructionLinks.StartPlot", e);
                    why = "a fault";
                    return 0;
                }
            }

            private static int StartWorks(string street, int number, int kind, bool mine, int contractor, int hours, out string why)
            {
                why = null;
                try
                {
                    CivicWorks works = Works();
                    if (works == null || !Settings.CivicConstruction)
                    {
                        why = "the city's construction sites are off";
                        return 0;
                    }
                    CityBuildingController place = CivicCity.Find(street, number);
                    if (place == null)
                    {
                        why = "no building at " + street + " " + number;
                        return 0;
                    }
                    SiteState site = works.StartAt((SiteKind)Mathf.Clamp(kind, 0, (int)SiteKind.Rebuild), place, TimeHelper.CurrentDay, mine, hours);
                    if (site == null)
                    {
                        why = "a site is there already, or the building has no street door";
                        return 0;
                    }
                    site.Contractor = Mathf.Clamp(contractor, 0, CivicCatalogue.Contractors.Length - 1);
                    return site.Id;
                }
                catch (Exception e)
                {
                    Fault("ConstructionLinks.StartWorks", e);
                    why = "a fault";
                    return 0;
                }
            }

            private static void SetContractor(int id, int rival)
            {
                try
                {
                    SiteState site = Works()?.Find(id);
                    if (site != null)
                    {
                        site.Contractor = Mathf.Clamp(rival, 0, CivicCatalogue.Contractors.Length - 1);
                    }
                }
                catch (Exception e)
                {
                    Fault("ConstructionLinks.SetContractor", e);
                }
            }

            // A test's lot: its ground clear of every building, as the programme's own plot test asks.
            private static bool Ground(Vector3 middle, int yaw, int width, int depth, out float ground)
            {
                ground = middle.y;
                try
                {
                    if (CitySpots.Ground(middle, out float height))
                    {
                        ground = height;
                    }
                    var half = new Vector3(width * 0.5f, 5f, depth * 0.5f);
                    return CivicSpots.BoxFree(new Vector3(middle.x, ground + 5.5f, middle.z), half, Quaternion.Euler(0f, yaw, 0f), CivicSpots.Walls);
                }
                catch (Exception e)
                {
                    Fault("ConstructionLinks.Ground", e);
                    return false;
                }
            }

            // ------------------------------------------------------------------ the new district

            // Every lot of the district's plan, as the construction game reads one: open while its ground stands at this load.
            private static void UptownLotsInto(List<LotView> into)
            {
                into.Clear();
                try
                {
                    IReadOnlyList<Lot> all = UptownLots.All;
                    for (int i = 0; all != null && i < all.Count; i++)
                    {
                        Lot l = all[i];
                        into.Add(new LotView
                        {
                            Id = l.Id, Street = l.Street, Number = l.Number, X = l.X, Y = UptownLots.GroundY, Z = l.Z, Yaw = l.Yaw, Width = l.Width, Depth = l.Depth,
                            Class = (int)l.Class, First = l.First, Open = UptownLots.Open(l), Traffic = Math.Max(0, DistrictBuildings.TrafficOf(l.Id))
                        });
                    }
                }
                catch (Exception e)
                {
                    Fault("ConstructionLinks.Lots", e);
                    into.Clear();
                }
            }

            private static bool LotTaken(string lot)
            {
                try
                {
                    return DistrictBuildings.Taken(lot);
                }
                catch (Exception e)
                {
                    Fault("ConstructionLinks.LotTaken", e);
                    return true;   // not for sale when it cannot be told
                }
            }

            private static bool LotBuilt(string lot)
            {
                try
                {
                    return DistrictBuildings.Finished(lot);
                }
                catch (Exception e)
                {
                    Fault("ConstructionLinks.LotBuilt", e);
                    return false;
                }
            }

            // The district takes a building of the sizes its lot's class holds.
            private static bool SizeTaken(string lot, string size)
            {
                try
                {
                    Lot l = UptownLots.Get(lot);
                    IReadOnlyList<string> sizes = l != null ? l.Sizes : null;
                    for (int i = 0; sizes != null && i < sizes.Count; i++)
                    {
                        if (string.Equals(sizes[i], size, StringComparison.Ordinal))
                        {
                            return true;
                        }
                    }
                    return false;
                }
                catch (Exception e)
                {
                    Fault("ConstructionLinks.SizeTaken", e);
                    return true;
                }
            }

            private static bool Finish(string lot, string design, string size, int version, string type, int storeys, bool player, double price, out string why)
            {
                why = null;
                try
                {
                    return DistrictBuildings.Finish(lot, design, size, version, type, storeys, player ? FinishOwner.Player : FinishOwner.City, price, out why);
                }
                catch (Exception e)
                {
                    Fault("ConstructionLinks.Finish", e);
                    why = "a fault";
                    return false;
                }
            }

            // ------------------------------------------------------------------ the copy engine

            private static bool Designs(float width, float depth, string type, int most, List<DesignChoice> into)
            {
                into.Clear();
                try
                {
                    if (!BuildingCopies.Fitting(width, depth, type, most, Fits))
                    {
                        return !BuildingCopies.Available;   // off: known, nothing fits; else being measured
                    }
                    for (int i = 0; i < Fits.Count; i++)
                    {
                        DesignFit f = Fits[i];
                        into.Add(new DesignChoice
                        {
                            Name = f.Name, Size = f.Size, Version = f.Version, Type = f.Type, Height = f.Height, Width = f.Width, Depth = f.Depth, TotalSqm = f.TotalSqm
                        });
                    }
                    return true;
                }
                catch (Exception e)
                {
                    Fault("ConstructionLinks.Designs", e);
                    return true;
                }
            }

            private static bool Footprint(string street, int number, out float width, out float depth)
            {
                width = depth = 0f;
                try
                {
                    return BuildingCopies.Footprint(CivicCity.Find(street, number), out width, out depth);
                }
                catch (Exception e)
                {
                    Fault("ConstructionLinks.Footprint", e);
                    return false;
                }
            }

            // ------------------------------------------------------------------ money

            private static double BusinessRate()
            {
                try
                {
                    Lender bank = BankTerms.House;
                    return bank != null && CreditLedger.Ready ? ProductTerms.BusinessRate(CreditLedger.Value, BankTerms.OwnRate(bank.Settings)) : double.NaN;
                }
                catch (Exception e)
                {
                    Fault("ConstructionLinks.BusinessRate", e);
                    return double.NaN;
                }
            }

            private static void Credit(int kind, int amount)
            {
                try
                {
                    if (!CreditLedger.Ready || SaveGameManager.Current == null)
                    {
                        return;
                    }
                    CreditLedger.Register(Loan);   // again, in case the credit file was opened afresh since the city loaded
                    int day = SaveGameManager.Current.Day;
                    CreditLedger.Post(kind == 0 ? CreditKind.NewCredit : CreditKind.PaidOff, kind == 0 ? amount : 0, day, Loan.Key);
                }
                catch (Exception e)
                {
                    Fault("ConstructionLinks.Credit", e);
                }
            }

            private static void Accounts(List<ICreditAccount> into)
            {
                try
                {
                    _accountsBefore?.Invoke(into);
                }
                catch (Exception e)
                {
                    Fault("ConstructionLinks.Accounts (the accounts before)", e);
                }
                into.Add(Loan);
            }

            /// <summary>The construction loans as one of the credit file's accounts: what is owed on every project, and the day's instalments.</summary>
            private sealed class ConstructionLoan : ICreditAccount
            {
                public string Key => "construction";
                public string NameKey => "reallife_bld_loan_account";
                public float Balance => Sum(0);
                public float Limit => Sum(1);
                public float PaymentDue => Sum(2);

                private static float Sum(int what)
                {
                    BuildState state = Director != null ? Director.State : null;
                    long total = 0;
                    for (int i = 0; state != null && i < state.Projects.Count; i++)
                    {
                        ProjectFile p = state.Projects[i];
                        total += what == 0 ? p.LoanOwed : what == 1 ? Math.Max(p.LoanAgreed, p.LoanOwed) : p.LoanFrom > 0 ? p.LoanOwed / Math.Max(1,
                            MoneyRules.TermDays(GameCalendar.DaysPerYear)) : 0L;
                    }
                    return total;
                }
            }

            // ------------------------------------------------------------------ the trades

            private static bool DesignOf(string project, out int total, out int costPermille, out int storeys, out int units)
            {
                total = storeys = units = 0;
                costPermille = 1000;
                try
                {
                    if (!TradesBridges.DesignOf(project, out DesignScore score))
                    {
                        return false;
                    }
                    total = score.Total;
                    costPermille = score.CostPermille;
                    storeys = score.Storeys;
                    units = score.Units;
                    return true;
                }
                catch (Exception e)
                {
                    Fault("ConstructionLinks.DesignOf", e);
                    return false;
                }
            }

            // ------------------------------------------------------------------ the city around the company

            // The city's government while its elections run: the one council that hears planning business.
            private static CivicGovernment Government()
            {
                CivicDirector civic = CivicDirector.Instance;
                return civic != null && civic.Running && Settings.CivicElections ? civic.Government : null;
            }

            private static bool CodeAhead(out int position, out int day, out int election)
            {
                position = day = election = 0;
                try
                {
                    CivicGovernment g = Government();
                    if (g == null)
                    {
                        return false;
                    }
                    election = g.State.ElectionDay;
                    for (int i = 0; i < g.State.Pending.Count; i++)
                    {
                        PendingChange change = g.State.Pending[i];
                        if (change.Lever == (int)Lever.Permits)
                        {
                            position = change.Position;
                            day = change.Day;
                            return true;
                        }
                    }
                    return false;
                }
                catch (Exception e)
                {
                    Fault("ConstructionLinks.CodeAhead", e);
                    return false;
                }
            }

            private static int CouncilDay(int day)
            {
                try
                {
                    CivicGovernment g = Government();
                    return g != null ? g.PlanningDay(day) : LandRules.CouncilDay(day);
                }
                catch (Exception e)
                {
                    Fault("ConstructionLinks.CouncilDay", e);
                    return LandRules.CouncilDay(day);
                }
            }

            private static int Council(string lot, int lotClass, int use, int suit, int reputation, bool now, out int ayes, out int noes)
            {
                ayes = noes = 0;
                try
                {
                    CivicGovernment g = Government();
                    return g != null ? g.Hear(lot, lotClass, use, suit, reputation, now, TimeHelper.CurrentDay, out ayes, out noes) : -2;
                }
                catch (Exception e)
                {
                    Fault("ConstructionLinks.Council", e);
                    return -2;
                }
            }

            private static int Followers()
            {
                try
                {
                    FlareFile file = FlareDirector.Instance != null ? FlareDirector.Instance.File : null;
                    return file != null ? file.Followers : 0;
                }
                catch (Exception e)
                {
                    Fault("ConstructionLinks.Followers", e);
                    return 0;
                }
            }

            // ------------------------------------------------------------------ the paper

            private static void Paper(int day, List<NewsItem> into)
            {
                try
                {
                    _paperBefore?.Invoke(day, into);
                }
                catch (Exception e)
                {
                    Fault("ConstructionLinks.Paper (the pieces before)", e);
                }
                try
                {
                    ConstructionDirector d = Director;
                    if (d == null || !d.Running)
                    {
                        return;
                    }
                    Stories.Clear();
                    d.Stories(day, Stories);
                    for (int i = 0; i < Stories.Count; i++)
                    {
                        BuildStory s = Stories[i];
                        into.Add(new NewsItem
                        {
                            Key = s.Key, Data = new Dictionary<string, string>(2) { { "name", s.Name ?? string.Empty }, { "count",
                                s.Count.ToString(System.Globalization.CultureInfo.InvariantCulture) } }
                        });
                    }
                    Stories.Clear();
                }
                catch (Exception e)
                {
                    Fault("ConstructionLinks.Paper", e);
                }
            }
        }
    }
}
