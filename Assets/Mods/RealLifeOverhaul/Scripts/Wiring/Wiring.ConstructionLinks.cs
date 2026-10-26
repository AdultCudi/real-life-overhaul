using System;
using System.Collections.Generic;
using Localizor;
using RealLifeOverhaul.BuildTrades;
using RealLifeOverhaul.Construction;
using RealLifeOverhaul.Designs;
using RealLifeOverhaul.Economy;
using RealLifeOverhaul.Events;
using RealLifeOverhaul.Fame;
using RealLifeOverhaul.Flare;
using RealLifeOverhaul.Interiors;
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
        ///   MaterialFactor            the raw materials exchange (Markets): a material's move, its list price following the price level
        ///   RivalNews ... UndoTests   the city's builders on the stock market and a project's hedge (Wiring.MarketsBuildLinks)
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
                SiteBuilders.Handed = (site, day) => Director?.SiteHanded(site.Id, day);
                SiteBuilders.Started = site => Director?.SiteStarted(Facts(site));
                SiteBuilders.Design = site => Director != null ? Director.DesignFor(site.Id) : null;
                SiteBuilders.Tag = site => Director != null ? Director.TagFor(site.Id) : null;
                SiteBuilders.KeepsLook = site => Director != null && Director.KeepsLook(site.Id);
                SiteBuilders.Taken = ConstructionDirector.LotBuilt;
                SiteBuilders.CopyOver = (building, design) => BuildingCopies.PlaceOver(building, design, null, Owner, true);
                SiteBuilders.CopyOnPlot = (design, middle, yaw, width, depth) => BuildingCopies.PlaceOnPlot(design, null, middle, yaw, width, depth, Owner);
                SiteBuilders.CopyRemove = handle => BuildingCopies.RemoveOutOfView(handle as CopyHandle);
                SiteBuilders.CopyDrop = handle => BuildingCopies.Remove(handle as CopyHandle);
                DistrictBuildings.GroundGiven = GroundGiven;
                SiteBuilders.CopyGone = handle => !(handle is CopyHandle copy) || copy.Gone;
                SiteBuilders.PlanningTopic = PlanningCall.Topic;
                SiteBuilders.Notice = PlanningCall.Notice;

                ConstructionBridges.Lots = UptownLotsInto;
                ConstructionBridges.LotsStamp = () => UptownLots.Stamp * 397 + DistrictBuildings.Stamp;
                ConstructionBridges.DistrictLives = () => DistrictBuildings.Lives;
                ConstructionBridges.LotTaken = LotTaken;
                ConstructionBridges.LotBuilt = LotBuilt;
                ConstructionBridges.LotRetired = UptownLots.OfEarlierPlan;
                ConstructionBridges.SizeTaken = SizeTaken;
                ConstructionBridges.LandPrice = () => DistrictBuildings.LandPricePerSqm;
                ConstructionBridges.Finish = Finish;
                ConstructionBridges.FinishOver = FinishOver;
                ConstructionBridges.SpacesOf = SpacesRules.SpacesOf;
                ConstructionBridges.CornerOffers = CornerLinks.Offers;   // the corner buildings that fit a corner site (Wiring.CornerLinks)
                ConstructionBridges.CornerName = CornerLinks.Name;
                BuildModeLinks.Connect();   // build mode: the district's demand, the ghost, the whole-building check (Wiring.BuildModeLinks)
                ConstructionBridges.StandingAt = StandingAt;
                ConstructionBridges.StandingOn = StandingOn;
                ConstructionBridges.Demolish = Demolish;
                ConstructionBridges.Cleared = Cleared;
                ConstructionBridges.HideLook = HideLook;
                ConstructionBridges.Extend = Extend;
                ConstructionBridges.Convert = Convert;
                ConstructionBridges.LetUnit = LetUnit;
                ConstructionBridges.Retraffic = Retraffic;
                ConstructionBridges.StandingAll = StandingAll;
                ConstructionBridges.Holdings = Holdings;
                DistrictBuildings.Lessee = RivalLease;   // whatever the options say: every machine leases the same offices
                ConstructionBridges.RivalOffice = RivalOffice;
                ConstructionBridges.RivalOfficeGo = RivalOfficeGo;
                ConstructionBridges.RivalHere = RivalHere;
                ConstructionBridges.RivalNearest = RivalNearest;
                ConstructionBridges.Designs = Designs;
                ConstructionBridges.Footprint = Footprint;
                ConstructionBridges.StartPlot = StartPlot;
                ConstructionBridges.StartWorks = StartWorks;
                ConstructionBridges.Site = Site;
                ConstructionBridges.AllSites = AllSites;
                ConstructionBridges.SetContractor = SetContractor;
                ConstructionBridges.SiteDone = SiteDone;
                ConstructionBridges.FinishSite = id => Works() is CivicWorks w && w.Finish(w.Find(id), TimeHelper.CurrentDay);
                ConstructionBridges.DropSite = id => Works() is CivicWorks w && w.Drop(w.Find(id));
                ConstructionBridges.SiteCount = () => Works() is CivicWorks w ? w.State.Sites.Count : 0;
                ConstructionBridges.AdvanceSite = id => Works() is CivicWorks w && w.Find(id) is SiteState s && w.AdvanceStage(s, TimeHelper.CurrentDay);
                ConstructionBridges.Ground = Ground;
                ConstructionBridges.Permits = () => Works() is CivicWorks w ? Mathf.Clamp(w.Permits, -1, 1) : 0;
                ConstructionBridges.BusinessRate = BusinessRate;
                ConstructionBridges.BankName = () => BankTerms.House != null ? BankTerms.House.Name : null;
                ConstructionBridges.Credit = Credit;
                ConstructionBridges.MaterialFactor = item => Markets.MarketDirector.Instance != null ? Markets.MarketDirector.Instance.MaterialMove(item) : double.NaN;
                ConstructionBridges.Record = () => Standing.Convictions > 0;
                ConstructionBridges.Lawyer = () => JusticeState.Loaded && JusticeState.Lawyer == LawyerKind.Firm;
                ConstructionBridges.Claim = BuildClaims.File;
                ConstructionBridges.ClaimDue = DebtClaims.DueDay;
                ConstructionBridges.Bribery = TaxCases.FileBribery;
                ConstructionBridges.Opened = (place, name) => FlareDirector.Instance?.Happened(FlareSource.Opened, -1, place, name, 0);
                ConstructionBridges.Standing = (points, scandal) => FameDirector.Instance?.Moved(scandal ? FamePart.Integrity
                    : FamePart.Neighbourhood, scandal ? FameCause.Scandal : FameCause.Press, points);
                ConstructionBridges.Storm = () => Weather.Sky == Sky.Storm;
                ConstructionBridges.Qualified = TradesBridges.Qualified;
                ConstructionBridges.Commission = (project, storeys, units, budget) => TradesBridges.Commission(project, storeys, units, (int)Math.Min(int.MaxValue, Math.Max(0L, budget)));
                ConstructionBridges.DesignOf = DesignOf;
                ConstructionBridges.RivalNews = MarketsBuildLinks.RivalNews;
                ConstructionBridges.HedgePlan = MarketsBuildLinks.HedgePlan;
                ConstructionBridges.Hedge = MarketsBuildLinks.Hedge;
                ConstructionBridges.Contract = MarketsBuildLinks.Contract;
                ConstructionBridges.SettleTest = MarketsBuildLinks.SettleTest;
                ConstructionBridges.UndoTests = MarketsBuildLinks.UndoTests;
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
                ConstructionBridges.ShowDesigns = ShowDesigns;
                ConstructionBridges.ShowDesign = ShowFind;
                ConstructionBridges.Standard = Standard;
                ConstructionBridges.Refitted = Refitted;
                ConstructionBridges.CloseShop = CloseShop;
                ConstructionBridges.OpenShop = OpenShop;
                ConstructionBridges.Interior = Interior;
                ConstructionBridges.KeepInterior = KeepInterior;
                ConstructionBridges.InteriorsBack = () => InteriorDirector.Instance != null ? InteriorDirector.Instance.TestPutBack() : 0;
                InteriorBridges.Renovate = Renovate;
                InteriorBridges.Renovation = Renovation;
                InteriorBridges.Renovates = () => Director != null && Director.RefitsOn;
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
                BuildModeLinks.Disconnect();
                SiteBuilders.Reset();
                DistrictBuildings.GroundGiven = null;
                DistrictBuildings.Lessee = null;
                ConstructionBridges.RivalOffice = null;
                ConstructionBridges.RivalOfficeGo = null;
                ConstructionBridges.RivalHere = null;
                ConstructionBridges.RivalNearest = null;
                ConstructionBridges.Lots = null;
                ConstructionBridges.LotsStamp = null;
                ConstructionBridges.DistrictLives = null;
                ConstructionBridges.LotTaken = null;
                ConstructionBridges.LotBuilt = null;
                ConstructionBridges.LotRetired = null;
                ConstructionBridges.SizeTaken = null;
                ConstructionBridges.LandPrice = null;
                ConstructionBridges.Finish = null;
                ConstructionBridges.FinishOver = null;
                ConstructionBridges.SpacesOf = null;
                ConstructionBridges.CornerOffers = null;
                ConstructionBridges.CornerName = null;
                ConstructionBridges.StandingAt = null;
                ConstructionBridges.StandingOn = null;
                ConstructionBridges.Demolish = null;
                ConstructionBridges.Cleared = null;
                ConstructionBridges.HideLook = null;
                ConstructionBridges.Extend = null;
                ConstructionBridges.Convert = null;
                ConstructionBridges.LetUnit = null;
                ConstructionBridges.Retraffic = null;
                ConstructionBridges.StandingAll = null;
                ConstructionBridges.Holdings = null;
                ConstructionBridges.Designs = null;
                ConstructionBridges.Footprint = null;
                ConstructionBridges.StartPlot = null;
                ConstructionBridges.StartWorks = null;
                ConstructionBridges.Site = null;
                ConstructionBridges.AllSites = null;
                ConstructionBridges.SetContractor = null;
                ConstructionBridges.SiteDone = null;
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
                ConstructionBridges.ClaimDue = null;
                ConstructionBridges.Bribery = null;
                ConstructionBridges.Opened = null;
                ConstructionBridges.Standing = null;
                ConstructionBridges.Storm = null;
                ConstructionBridges.Qualified = null;
                ConstructionBridges.Commission = null;
                ConstructionBridges.DesignOf = null;
                ConstructionBridges.RivalNews = null;
                ConstructionBridges.HedgePlan = null;
                ConstructionBridges.Hedge = null;
                ConstructionBridges.Contract = null;
                ConstructionBridges.SettleTest = null;
                ConstructionBridges.UndoTests = null;
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
                ConstructionBridges.ShowDesigns = null;
                ConstructionBridges.ShowDesign = null;
                ConstructionBridges.Standard = null;
                ConstructionBridges.Refitted = null;
                ConstructionBridges.CloseShop = null;
                ConstructionBridges.OpenShop = null;
                ConstructionBridges.Interior = null;
                ConstructionBridges.KeepInterior = null;
                ConstructionBridges.InteriorsBack = null;
                InteriorBridges.Renovate = null;
                InteriorBridges.Renovation = null;
                InteriorBridges.Renovates = null;
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

            // at: the hour the programme works (day x 24 + hour), which a sleep's catching up works some time after.
            private static float Hours(SiteState site, float hours, long at)
            {
                try
                {
                    ConstructionDirector d = Director;
                    return d != null && site != null ? d.WorkOn(site.Id, site.Done, site.Needed, hours, (int)(at / 24), (int)(at % 24)) : hours;
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
                    Contractor = site.Contractor, Known = true, Mine = site.Mine
                };
            }

            // ------------------------------------------------------------------ the builders ask the programme

            // Every site of the programme, for build mode's plots under construction: once at an opening.
            private static void AllSites(List<SiteFacts> into)
            {
                into.Clear();
                try
                {
                    CivicWorks works = Works();
                    for (int i = 0; works != null && i < works.State.Sites.Count; i++)
                    {
                        into.Add(Facts(works.State.Sites[i]));
                    }
                }
                catch (Exception e)
                {
                    Fault("ConstructionLinks.AllSites", e);
                }
            }

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
                    SiteState site = works.StartPlot(middle, yaw, width, depth, storeys, TimeHelper.CurrentDay, out why, hours);   // set out with its builder's hours, as its line says
                    if (site == null)
                    {
                        return 0;
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
                    // A plot is set out on its ground, never at a building; a demolition is the one kind after it asked for at one.
                    SiteKind asked = kind == (int)SiteKind.Demolition ? SiteKind.Demolition : (SiteKind)Mathf.Clamp(kind, 0, (int)SiteKind.Rebuild);
                    SiteState site = works.StartAt(asked, place, TimeHelper.CurrentDay, mine, hours);
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

            // A site set out again for a project starts where its works had reached, short of its end (the programme hands over
            // a finished one, which only its works may do).
            private static void SiteDone(int id, float done)
            {
                try
                {
                    SiteState site = Works()?.Find(id);
                    if (site != null)
                    {
                        site.Done = Mathf.Clamp(done, 0f, site.Needed * 0.999f);
                        Works()?.SitesChanged?.Invoke();   // what stands in the street follows its stage
                    }
                }
                catch (Exception e)
                {
                    Fault("ConstructionLinks.SiteDone", e);
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
                        bool corners = LayoutRules.StreetEnds(l.Row, l.Column, (int)l.Facing, out float low, out float high);   // the block's corners on an avenue's strip
                        into.Add(new LotView
                        {
                            Id = l.Id, Street = l.Street, Number = l.Number, X = l.X, Y = UptownLots.GroundY, Z = l.Z, Yaw = l.Yaw, Width = l.Width, Depth = l.Depth,
                            Class = (int)l.Class, First = l.First, Open = UptownLots.Open(l), Traffic = Math.Max(0, DistrictBuildings.TrafficOf(l.Id)), Block = l.Block,
                            Strip = l.Strip, Index = l.Index, Spaces = l.Spaces, Size = l.Size, StreetLow = corners, StreetHigh = corners, EdgeLow = low, EdgeHigh = high
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

            // A site of several spaces: its building over all of them, as a landmark's stands over its block's lots.
            private static bool FinishOver(string lot, IList<string> over, string design, string size, int version, string type, int storeys, bool player, double price,
                                           string look, out string why)
            {
                why = null;
                try
                {
                    bool done = DistrictBuildings.FinishOver(lot, over, design, size, version, type, storeys, player ? FinishOwner.Player : FinishOwner.City, price, look, out why);
                    return done && Stand(lot);
                }
                catch (Exception e)
                {
                    Fault("ConstructionLinks.FinishOver", e);
                    why = "a fault";
                    return false;
                }
            }

            // A building handed to the district stands now, as the scaffolding comes down (DistrictBuildings.StandNow): at once out of view,
            // else at a black screen. One rule for every finished building the construction game hands over, a project's, a rival's
            // development and the city's redevelopment. Handed over all the same when it cannot: it then stands from the next load.
            private static bool Stand(string lot)
            {
                try
                {
                    DistrictBuildings.StandNow(lot, out _);
                }
                catch (Exception e)
                {
                    Fault("ConstructionLinks.StandNow", e);
                }
                return true;
            }

            // The construction sites' props and the finished building's look over a ground a building has just stood on come down in the same moment.
            private static void GroundGiven(string lot, float x1, float z1, float x2, float z2)
            {
                try
                {
                    CivicDirector.Instance?.GroundGiven(x1, z1, x2, z2);
                }
                catch (Exception e)
                {
                    Fault("ConstructionLinks.GroundGiven", e);
                }
            }

            private static bool Finish(string lot, string design, string size, int version, string type, int storeys, bool player, double price, string look, out string why)
            {
                why = null;
                try
                {
                    bool done = DistrictBuildings.Finish(lot, design, size, version, type, storeys, player ? FinishOwner.Player : FinishOwner.City, price, look, out why);
                    return done && Stand(lot);
                }
                catch (Exception e)
                {
                    Fault("ConstructionLinks.Finish", e);
                    why = "a fault";
                    return false;
                }
            }

            // ------------------------------------------------------------------ redevelopment: the district's buildings

            private static bool StandingAt(string street, int number, out StandingView view)
            {
                view = default(StandingView);
                try
                {
                    return DistrictBuildings.StandingAt(street, number, out DistrictBuilding b) && View(b, out view);
                }
                catch (Exception e)
                {
                    Fault("ConstructionLinks.StandingAt", e);
                    return false;
                }
            }

            private static bool StandingOn(string lot, out StandingView view)
            {
                view = default(StandingView);
                try
                {
                    return DistrictBuildings.StandingOn(lot, out DistrictBuilding b) && View(b, out view);
                }
                catch (Exception e)
                {
                    Fault("ConstructionLinks.StandingOn", e);
                    return false;
                }
            }

            private static bool View(DistrictBuilding b, out StandingView view)
            {
                view = new StandingView
                {
                    Lot = b.Lot, Street = b.Street, Number = b.Number, Size = b.Size, Version = b.Version, Type = b.Type, Design = b.Design, Storeys = b.Storeys,
                    SizeSqm = b.SizeSqm, Sqm = b.Sqm, Height = b.Height, Over = b.Over, Finished = b.Finished, Kept = b.Kept
                };
                return true;
            }

            private static string Demolish(string lot)
            {
                try
                {
                    return DistrictBuildings.Demolish(lot, out string why) ? null : why ?? "refused";
                }
                catch (Exception e)
                {
                    Fault("ConstructionLinks.Demolish", e);
                    return "a fault";
                }
            }

            private static bool Cleared(string lot)
            {
                try
                {
                    return DistrictBuildings.Cleared(lot);
                }
                catch (Exception e)
                {
                    Fault("ConstructionLinks.Cleared", e);
                    return false;
                }
            }

            private static bool HideLook(string lot, bool hide)
            {
                try
                {
                    return DistrictBuildings.HideLook(lot, hide);
                }
                catch (Exception e)
                {
                    Fault("ConstructionLinks.HideLook", e);
                    return false;
                }
            }

            private static readonly List<DistrictBuilding> AllStanding = new List<DistrictBuilding>(256);

            private static void StandingAll(List<StandingView> into)
            {
                into.Clear();
                try
                {
                    DistrictBuildings.StandingAll(AllStanding);
                    for (int i = 0; i < AllStanding.Count; i++)
                    {
                        if (View(AllStanding[i], out StandingView view))
                        {
                            into.Add(view);
                        }
                    }
                }
                catch (Exception e)
                {
                    Fault("ConstructionLinks.StandingAll", e);
                }
                finally
                {
                    AllStanding.Clear();
                }
            }

            // The rival tycoons in play, by their holdings' names: each may develop in Uptown as the city's builders do.
            private static void Holdings(List<string> into)
            {
                into.Clear();
                try
                {
                    Rivals.RivalDirector rivals = Rivals.RivalDirector.Instance;
                    Rivals.RivalFile f = rivals != null && rivals.Running && !rivals.Resting ? rivals.File : null;
                    for (int i = 0; f != null && i < f.Tycoons.Length; i++)
                    {
                        if (f.Tycoons[i] != null && f.Tycoons[i].Active)
                        {
                            into.Add(f.Tycoons[i].Def.Holding);
                        }
                    }
                }
                catch (Exception e)
                {
                    Fault("ConstructionLinks.Holdings", e);
                }
            }

            private static string Extend(string lot, int storeys)
            {
                try
                {
                    return DistrictBuildings.Extend(lot, storeys, out string why) ? null : why ?? "refused";
                }
                catch (Exception e)
                {
                    Fault("ConstructionLinks.Extend", e);
                    return "a fault";
                }
            }

            private static string LetUnit(string lot)
            {
                try
                {
                    return DistrictBuildings.LetUnit(lot);
                }
                catch (Exception e)
                {
                    Fault("ConstructionLinks.LetUnit", e);
                    return "a fault";
                }
            }

            private static bool Retraffic(string lot, int drop)
            {
                try
                {
                    return DistrictBuildings.Retraffic(lot, drop);
                }
                catch (Exception e)
                {
                    Fault("ConstructionLinks.Retraffic", e);
                    return false;
                }
            }

            private static string Convert(string lot, string type)
            {
                try
                {
                    return DistrictBuildings.Convert(lot, type, out string why) ? null : why ?? "refused";
                }
                catch (Exception e)
                {
                    Fault("ConstructionLinks.Convert", e);
                    return "a fault";
                }
            }

            // ------------------------------------------------------------------ the rival builders' offices in Uptown

            private static readonly DistrictLease[] RivalLeases = new DistrictLease[BuildCatalogue.RivalCount];

            // A rival builder's office (the rival's index is its lease's): a business of the construction company's type, set up as
            // the game's design studios are, under the rival's name (a proper name, the same in every language), valued by its crews.
            private static DistrictLease RivalLease(int rival)
            {
                try
                {
                    if (rival < 0 || rival >= RivalLeases.Length)
                    {
                        return null;
                    }
                    return RivalLeases[rival] ?? (RivalLeases[rival] = new DistrictLease
                    {
                        Owner = Owner, BusinessType = ConstructionType.TypeName, LayoutsFrom = ConstructionType.Template, Name = ConstructionText.T(BuildCatalogue.RivalKeys[rival]),
                        DescriptionKey = BuildCatalogue.OfficeAboutKey, DailyIncome = BuildCatalogue.RivalCrews[rival] * BuildCatalogue.OfficeDayPerCrew
                    });
                }
                catch (Exception e)
                {
                    Fault("ConstructionLinks.RivalLease", e);
                    return null;
                }
            }

            private static string RivalOffice(int rival)
            {
                try
                {
                    return DistrictBuildings.LeaseStands(rival, out string address) ? address : null;
                }
                catch (Exception e)
                {
                    Fault("ConstructionLinks.RivalOffice", e);
                    return null;
                }
            }

            private static bool RivalOfficeGo(int rival)
            {
                try
                {
                    return DistrictBuildings.LeaseDestination(rival);
                }
                catch (Exception e)
                {
                    Fault("ConstructionLinks.RivalOfficeGo", e);
                    return false;
                }
            }

            // The rival whose office the player is inside, while it may receive them; asked once a second by the front desk.
            private static int RivalHere()
            {
                try
                {
                    return DistrictBuildings.LeaseHere();
                }
                catch (Exception e)
                {
                    Fault("ConstructionLinks.RivalHere", e);
                    return -1;
                }
            }

            private static int RivalNearest(float x, float z)
            {
                try
                {
                    return DistrictBuildings.LeaseNearest(x, z);
                }
                catch (Exception e)
                {
                    Fault("ConstructionLinks.RivalNearest", e);
                    return -1;
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

            // ------------------------------------------------------------------ show flats: the designs and the district's standard

            private static readonly List<ShowDesign> Shows = new List<ShowDesign>(8);

            // The show flats' designs (Designs) for flats of a floor area, each scored by Interiors as a business's decor is.
            private static bool ShowDesigns(int flatArea, List<ShowDesignView> into)
            {
                into.Clear();
                try
                {
                    if (!DesignsDirector.ShowDesigns(flatArea, Shows))
                    {
                        return false;
                    }
                    for (int i = 0; i < Shows.Count; i++)
                    {
                        into.Add(View(Shows[i]));
                    }
                    return into.Count > 0;
                }
                catch (Exception e)
                {
                    into.Clear();
                    Fault("ConstructionLinks.ShowDesigns", e);
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

            private static bool ShowFind(string id, out ShowDesignView view)
            {
                view = default(ShowDesignView);
                try
                {
                    ShowDesign d = DesignsDirector.FindShow(id);
                    if (d == null)
                    {
                        return false;
                    }
                    view = View(d);
                    return true;
                }
                catch (Exception e)
                {
                    Fault("ConstructionLinks.ShowDesign", e);
                    return false;
                }
            }

            private static ShowDesignView View(ShowDesign d)
            {
                string name = ConstructionText.T(d.Finish == ShowFinish.Plain ? "reallife_bld_show_design_plain" : "reallife_bld_show_design_staged", "design",
                    ("interior_design_" + d.Design.ToLowerInvariant()).GetLocalization());
                return new ShowDesignView
                {
                    Id = d.Id, Name = name, Size = d.Size, Version = d.Version, Sqm = d.Sqm, Design = d.Design, Interior = d.Interior, Decor = InteriorDirector.DecorOf(d.Names, d.Sqm),
                    Furniture = d.Furniture, Fee = d.Fee, Pieces = d.Names.Count, Keeps = d.Keeps
                };
            }

            // The standard a district expects of an interior; a plot's (no district of the game's) is the new district's while it lives.
            private static int Standard(string district)
            {
                try
                {
                    string at = !string.IsNullOrEmpty(district) ? district : DistrictBuildings.Lives ? DistrictValues.Id : null;
                    return at != null ? InteriorDirector.StandardOf(at) : -1;
                }
                catch (Exception e)
                {
                    Fault("ConstructionLinks.Standard", e);
                    return -1;
                }
            }

            // ------------------------------------------------------------------ refits: the business, its interior, its closure

            private static BuildingRegistration Shop(string street, int number)
            {
                return BusinessGame.Held(BusinessState.PlaceKey(street, number));
            }

            private static bool Refitted(string street, int number, int grade)
            {
                try
                {
                    BuildingRegistration reg = Shop(street, number);
                    return reg != null && InteriorDirector.Instance != null && InteriorDirector.Instance.Running && InteriorDirector.Instance.Refit(reg, grade);
                }
                catch (Exception e)
                {
                    Fault("ConstructionLinks.Refitted", e);
                    return false;
                }
            }

            private static bool CloseShop(string street, int number, int hours)
            {
                try
                {
                    BuildingRegistration reg = Shop(street, number);
                    return reg != null && BusinessDirector.Instance != null && BusinessDirector.Instance.CloseFor(reg, hours, ClosureSource.Refit);
                }
                catch (Exception e)
                {
                    Fault("ConstructionLinks.CloseShop", e);
                    return false;
                }
            }

            private static void OpenShop(string street, int number)
            {
                try
                {
                    BuildingRegistration reg = Shop(street, number);
                    if (reg != null)
                    {
                        BusinessDirector.Instance?.Release(reg, ClosureSource.Refit);
                    }
                }
                catch (Exception e)
                {
                    Fault("ConstructionLinks.OpenShop", e);
                }
            }

            private static bool Interior(string street, int number, out int decor, out int refit, out int wear)
            {
                decor = refit = wear = 0;
                try
                {
                    BuildingRegistration reg = Shop(street, number);
                    return reg != null && InteriorDirector.DecorNow(reg, out decor, out refit, out wear);
                }
                catch (Exception e)
                {
                    Fault("ConstructionLinks.Interior", e);
                    return false;
                }
            }

            private static void KeepInterior(string street, int number)
            {
                try
                {
                    BuildingRegistration reg = Shop(street, number);
                    if (reg != null)
                    {
                        InteriorDirector.Instance?.TestKeep(reg);
                    }
                }
                catch (Exception e)
                {
                    Fault("ConstructionLinks.KeepInterior", e);
                }
            }

            // A renovation asked from the business's Interior tab, and the words for one under way there.
            private static void Renovate(BuildingRegistration reg)
            {
                try
                {
                    if (reg != null && reg.Address != null)
                    {
                        Director?.RenovateAsk(reg.Address.streetName, reg.Address.streetNumber);
                    }
                }
                catch (Exception e)
                {
                    Fault("ConstructionLinks.Renovate", e);
                }
            }

            private static string Renovation(BuildingRegistration reg)
            {
                try
                {
                    ConstructionDirector d = Director;
                    return d != null && d.RefitsOn && reg != null && reg.Address != null ? d.RenovationWords(reg.Address.streetName, reg.Address.streetNumber) : null;
                }
                catch (Exception e)
                {
                    Fault("ConstructionLinks.Renovation", e);
                    return null;
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
