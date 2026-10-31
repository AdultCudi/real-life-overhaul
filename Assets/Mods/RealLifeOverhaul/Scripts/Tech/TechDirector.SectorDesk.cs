using System;
using System.Collections.Generic;
using UI.Notification;

namespace RealLifeOverhaul.Tech
{
    /// <summary>
    /// The other tech firms' decisions: the fourteen desk sessions' callbacks (held at a firm's desk or by its manager,
    /// Wiring/Wiring.SectorLinks): what each answers, why it cannot be held now, a manager's sound choice, and the choices
    /// applied with their money booked at the firm and their news told; and the firms' pages as data (SectorViews), with the
    /// buttons those pages offer. Run on a click, a session's end or a page drawn, never per frame.
    /// </summary>
    public sealed partial class TechDirector
    {
        private const string DeskCostsKey = "reallife_transaction_tech_desk_costs";
        private const string DeskMod = "reallife", DeskKeys = DeskMod + "_tech_desk_";   // the keys' start, written so the source holds no key prefix as a literal

        // ---------------------------------------------------------------- what each session answers

        // A firm of the player's at a place that holds a kind (a data centre's kinds are held at any tech office or warehouse of the player's).
        private FirmPlace DeskFirm(int kind, string place)
        {
            FirmPlace firm = FirmOf(place);
            int want = SectorRules.DeskFirm[TalentCatalogue.Clamp(kind, 0, SectorRules.Desks - 1)];
            if (want < 0 || firm == null)
            {
                return firm;
            }
            bool agency = want == (int)FirmKind.WebAgency;
            return firm.File.Kind == want || agency && firm.File.Kind == (int)FirmKind.Design ? firm : null;
        }

        // The firm's product with the most customers on sale, of a market's buyers at least (Segment), or null.
        private Product LiveProduct(string place, int leastSegment, bool modelsOnly)
        {
            Product best = null;
            for (int i = 0; i < _firmsFile.Products.Count; i++)
            {
                Product p = _firmsFile.Products[i];
                if (p.Place == place && p.Live && SectorRules.MarketOf(p) >= 0 && p.Segment >= leastSegment && (!modelsOnly || p.K == ProductKind.Model)
                    && (best == null || SectorRules.Have(p) > SectorRules.Have(best)))
                {
                    best = p;
                }
            }
            return best;
        }

        private bool BuildsAny(string place)
        {
            for (int i = 0; i < _firmsFile.Products.Count; i++)
            {
                Product p = _firmsFile.Products[i];
                if (p.Place == place && p.Stage < 2 && SectorRules.MarketOf(p) >= 0)
                {
                    return true;
                }
            }
            return false;
        }

        private Product TrainingModel(string place)
        {
            for (int i = 0; i < _firmsFile.Products.Count; i++)
            {
                Product p = _firmsFile.Products[i];
                if (p.Place == place && p.K == ProductKind.Model && p.Stage < 2)
                {
                    return p;
                }
            }
            return null;
        }

        private Product LaunchModel(string place)
        {
            for (int i = 0; i < _firmsFile.Products.Count; i++)
            {
                Product p = _firmsFile.Products[i];
                if (p.Place == place && SectorRules.HasLaunch(p, _sector.ProductAt(p.Id)))
                {
                    return p;
                }
            }
            return null;
        }

        // An offer made to the firm, else (taking tenders) an open council tender of work it takes.
        private Contract PitchFor(FirmPlace firm, bool tenders)
        {
            Contract tender = null;
            for (int i = 0; i < _firmsFile.Contracts.Count; i++)
            {
                Contract w = _firmsFile.Contracts[i];
                if (w.Stage == (int)WorkStage.Offered && w.Place == firm.File.Place)
                {
                    return w;
                }
                if (tenders && tender == null && w.Stage == (int)WorkStage.Tender && FirmsRules.Takes(firm.File.K, w.K))
                {
                    tender = w;
                }
            }
            return tender;
        }

        private Contract ActiveWhere(string place, Func<Contract, bool> fits)
        {
            for (int i = 0; i < _firmsFile.Contracts.Count; i++)
            {
                Contract w = _firmsFile.Contracts[i];
                if (w.Place == place && fits(w))
                {
                    return w;
                }
            }
            return null;
        }

        private static bool OpenIncident(Contract w) => w.Stage == (int)WorkStage.Active && w.K == WorkKind.Incident && w.Done < w.Points;

        private static bool Finished(Contract w) => w.Stage == (int)WorkStage.Done;

        private DataCentre DownCentre(int day)
        {
            for (int i = 0; _infra != null && i < _infra.Centres.Count; i++)
            {
                if (SectorRules.DownLately(_infra.Centres[i], day))
                {
                    return _infra.Centres[i];
                }
            }
            return null;
        }

        private bool Seniors(string place)
        {
            for (int i = 0; i < _people.Count; i++)
            {
                Profile p = _people[i].File;
                if (p.Place == place && p.Level >= TalentCatalogue.Senior && p.NoticeEnd == 0 && FirmsRules.Builds(p.Def))
                {
                    return true;
                }
            }
            return false;
        }

        // ---------------------------------------------------------------- the callbacks

        /// <summary>Why a kind of the sector's sessions cannot be held at a business now (a key), or null.</summary>
        internal string SectorBlocked(int kind, string place)
        {
            if (!_sectorOn || _firmsFile == null || Save == null)
            {
                return DeskKeys + "why_off";
            }
            int day = Save.Day;
            FirmPlace firm = DeskFirm(kind, place);
            if (SectorRules.DeskFirm[TalentCatalogue.Clamp(kind, 0, SectorRules.Desks - 1)] < 0)
            {
                if (!HasCentres())
                {
                    return DeskKeys + "why_no_centre";
                }
                switch (kind)
                {
                    case SectorRules.DeskPower: return SectorRules.SpikeWaits(_sector, day) ? null : DeskKeys + "why_no_spike";
                    case SectorRules.DeskFailover: return DownCentre(day) != null ? null : DeskKeys + "why_no_failover";
                    default: return null;
                }
            }
            if (firm == null)
            {
                return DeskKeys + "why_off";
            }
            SectorFirm sf = _sector.FirmAt(place);
            switch (kind)
            {
                case SectorRules.DeskSprint: return BuildsAny(place) ? null : DeskKeys + "why_no_product";
                case SectorRules.DeskOutage: return sf != null && day - sf.LastOutage <= SectorCatalogue.OutageWindow && LiveProduct(place, 0, false) != null ? null : DeskKeys + "why_no_outage";
                case SectorRules.DeskBigCustomer:
                    Product big = LiveProduct(place, (int)Segment.Business, false);
                    return big != null && big.Users >= SectorCatalogue.BigCustomerLeast && (sf == null || day - sf.LastBigAsk >= SectorCatalogue.BigCustomerEvery) ? null : DeskKeys + "why_no_big";
                case SectorRules.DeskTraining:
                    Product model = TrainingModel(place);
                    return model == null ? DeskKeys + "why_no_model" : SectorRules.Held(_sector.ProductAt(model.Id), day) ? DeskKeys + "why_held" : null;
                case SectorRules.DeskLaunch: return LaunchModel(place) != null ? null : DeskKeys + "why_no_launch";
                case SectorRules.DeskPitch: return PitchFor(firm, true) != null ? null : DeskKeys + "why_no_pitch";
                case SectorRules.DeskScope: return ActiveWhere(place, SectorRules.Creeps) != null ? null : DeskKeys + "why_no_scope";
                case SectorRules.DeskIncident: return ActiveWhere(place, OpenIncident) != null ? null : DeskKeys + "why_no_incident";
                case SectorRules.DeskClientPitch: return PitchFor(firm, false) != null ? null : DeskKeys + "why_no_offer";
                case SectorRules.DeskRevision: return ActiveWhere(place, SectorRules.WantsChanges) != null ? null : DeskKeys + "why_no_revision";
                default: return ActiveWhere(place, Finished) != null ? null : DeskKeys + "why_no_portfolio";
            }
        }

        /// <summary>The sound choice at a step of a kind, as a manager judges it at a business.</summary>
        internal int SectorPick(int kind, string place, int step)
        {
            if (!_sectorOn || Save == null)
            {
                return 0;
            }
            int day = Save.Day;
            FirmPlace firm = FirmOf(place);
            SectorFirm sf = _sector.FirmAt(place);
            switch (kind)
            {
                case SectorRules.DeskSprint:
                    Product p = LiveProduct(place, 0, false);
                    int m = p != null ? SectorRules.MarketOf(p) : -1;
                    return SectorRules.PickSprint(step, firm != null ? firm.File.Debt : 0, sf != null && day - sf.LastOutage <= 7, m >= 0 && p.Features < _sector.Markets[m].Average,
                        p != null ? SectorRules.Soundness(p.Bugs, FirmsRules.Allowance(p)) : 1000);
                case SectorRules.DeskOutage: return SectorRules.PickOutage(step, sf != null ? sf.LostToOutage : 0);
                case SectorRules.DeskBigCustomer: return SectorRules.PickBigCustomer(firm != null ? firm.Crew.Engineering : 0);
                case SectorRules.DeskTraining: return SectorRules.PickTraining(step, OwnGpu());
                case SectorRules.DeskLaunch: return SectorRules.PickLaunch(step);
                case SectorRules.DeskPitch: return SectorRules.PickPitch(step, Seniors(place));
                case SectorRules.DeskScope: return SectorRules.PickScope(ActiveWhere(place, SectorRules.Creeps));
                case SectorRules.DeskIncident: return SectorRules.PickIncident(step, Seniors(place));
                case SectorRules.DeskCapacity: return SectorRules.PickCapacity(step, _infra.Cloud.Sold, CloudCapacity());
                case SectorRules.DeskPower: return SectorRules.PickPower(_sector, day);
                case SectorRules.DeskFailover: return SectorRules.PickFailover(step, InfraRules.Regions(_infra.Centres));
                case SectorRules.DeskClientPitch: return SectorRules.PickClientPitch(step);
                case SectorRules.DeskRevision: return SectorRules.PickRevision(ActiveWhere(place, SectorRules.WantsChanges));
                default: return SectorRules.PickPortfolio();
            }
        }

        // True when the player's cloud has AI accelerators to run a lab's training on.
        private bool OwnGpu()
        {
            if (_infra == null || !_infra.Cloud.Open)
            {
                return false;
            }
            for (int i = 0; i < _infra.Centres.Count; i++)
            {
                if (_infra.Centres[i].GpuRacks > 0)
                {
                    return true;
                }
            }
            return false;
        }

        private int CloudCapacity()
        {
            int units = 0;
            for (int i = 0; _infra != null && i < _infra.Centres.Count; i++)
            {
                units += InfraRules.Units(_infra.Centres[i]);
            }
            return units;
        }

        /// <summary>
        /// A kind of the sector's sessions held at a business with its choices, by the owner or a manager (their strength,
        /// thousandths): the firm's state changed, its money booked, its news told. False with nothing changed (it stays due);
        /// the key and pairs say the result in a notice either way.
        /// </summary>
        internal bool SectorSession(int kind, string place, string business, int strength, int[] choices, out string key, out string[] pairs)
        {
            pairs = new[] { "business", business ?? string.Empty };
            string why = SectorBlocked(kind, place);
            key = why ?? DeskKeys + "why_off";
            if (why != null || Save == null)
            {
                return false;
            }
            int day = Save.Day, seed = _sector.Seed;
            FirmPlace firm = FirmOf(place);
            SectorFirm sf = _sector.FirmFor(place);
            string name = SectorRules.DeskIds[kind].Substring(5);
            key = DeskKeys + name + "_done";
            DeskResult r;
            string subject = business ?? string.Empty;
            switch (kind)
            {
                case SectorRules.DeskSprint:
                    r = SectorRules.Sprint(sf, SectorRules.Answer(choices, 0, 3, SectorRules.SprintBalanced), SectorRules.Answer(choices, 1, 2, SectorRules.ReleaseReady), day);
                    break;
                case SectorRules.DeskOutage:
                    Product down = LiveProduct(place, 0, false);
                    subject = down.Name;
                    r = SectorRules.Outage(sf, down, firm.File, SectorRules.Answer(choices, 0, 3, SectorRules.OutageStatus), SectorRules.Answer(choices, 1, 3, SectorRules.AfterPostMortem), seed, day, strength);
                    break;
                case SectorRules.DeskBigCustomer:
                    Product big = LiveProduct(place, (int)Segment.Business, false);
                    subject = big.Name;
                    r = SectorRules.BigCustomer(sf, big, SectorRules.Answer(choices, 0, 3, SectorRules.BigDiscount), day);
                    break;
                case SectorRules.DeskTraining:
                    Product model = TrainingModel(place);
                    subject = model.Name;
                    r = SectorRules.Training(_sector, model, _sector.ProductFor(model.Id), firm.File, SectorRules.Answer(choices, 0, 3, SectorRules.ComputeRented),
                        SectorRules.Answer(choices, 1, 3, SectorRules.DataLicensed), SectorRules.Answer(choices, 2, 3, SectorRules.SafetyStandard), OwnGpu(), seed, day, strength);
                    break;
                case SectorRules.DeskLaunch:
                    Product launch = LaunchModel(place);
                    subject = launch.Name;
                    r = SectorRules.Launch(launch, _sector.ProductFor(launch.Id), firm.File, SectorRules.Answer(choices, 0, 3, SectorRules.ReleaseApi), SectorRules.Answer(choices, 1, 3, SectorRules.PriceFair), seed, day);
                    if (r.Launched)
                    {
                        Story("reallife_tech_news_launch", "product", launch.Name, "firm", firm.File.Name);
                        SectorBridges.Say(SectorNews.Launch, place, launch.Name, launch.Features);
                    }
                    break;
                case SectorRules.DeskPitch:
                    Contract pitch = PitchFor(firm, true);
                    subject = pitch.Client;
                    r = SectorRules.Pitch(pitch, sf, place, SectorRules.Answer(choices, 0, 3, SectorRules.StaffMixed), SectorRules.Answer(choices, 1, 3, SectorRules.PriceFair), Seniors(place), seed, day, strength);
                    if (r.Income > 0)
                    {
                        ChargeClient(pitch, firm, r.Income * Prices.Level / 1000, null);
                        TellIfNobody(pitch);
                    }
                    key = DeskKeys + name + (r.Figure == 1 ? "_done_bid" : r.Figure == 2 ? "_done" : "_done_lost");
                    break;
                case SectorRules.DeskScope:
                    Contract scope = ActiveWhere(place, SectorRules.Creeps);
                    subject = scope.Client;
                    r = SectorRules.Scope(scope, firm.File, SectorRules.Answer(choices, 0, 3, SectorRules.ScopeAbsorb));
                    break;
                case SectorRules.DeskIncident:
                    Contract incident = ActiveWhere(place, OpenIncident);
                    subject = incident.Client;
                    r = SectorRules.Incident(incident, firm.File, SectorRules.Answer(choices, 0, 3, SectorRules.ContainIsolate), SectorRules.Answer(choices, 1, 3, SectorRules.TellNow), Seniors(place), seed, day, strength);
                    break;
                case SectorRules.DeskCapacity:
                    r = SectorRules.Capacity(_sector, _infra, SectorRules.Answer(choices, 0, 3, SectorRules.AheadAsNeeded), SectorRules.Answer(choices, 1, 3, SectorRules.PriceFair), BuyScale, day);
                    if (r.Racks > 0 && !BuyAhead((int)r.Racks, r.Cost))
                    {
                        key = DeskKeys + "capacity_done_cash";
                        r.Racks = 0;
                    }
                    r.Cost = 0;   // racks are capital, paid by BuyAhead
                    break;
                case SectorRules.DeskPower:
                    r = SectorRules.Power(_sector, SectorRules.Answer(choices, 0, 3, SectorRules.PowerRide), MonthPower(), day);
                    break;
                case SectorRules.DeskFailover:
                    DataCentre centre = DownCentre(day);
                    subject = centre.Name;
                    r = SectorRules.Failover(_sector, _infra.Cloud, InfraRules.Regions(_infra.Centres), SectorRules.Answer(choices, 0, 3, SectorRules.FailRestore),
                        SectorRules.Answer(choices, 1, 3, SectorRules.AfterPostMortem), CloudDayTakings(), day);
                    centre.DownUntil = Math.Min(centre.DownUntil, day - SectorCatalogue.OutageWindow - 1);   // answered: its failover is not asked again
                    break;
                case SectorRules.DeskClientPitch:
                    Contract offer = PitchFor(firm, false);
                    subject = offer.Client;
                    r = SectorRules.ClientPitch(offer, place, SectorRules.Answer(choices, 0, 3, SectorRules.ConceptSafe), SectorRules.Answer(choices, 1, 2, SectorRules.PaceUsual), seed, day);
                    if (r.Income > 0)
                    {
                        ChargeClient(offer, firm, r.Income * Prices.Level / 1000, null);
                        TellIfNobody(offer);
                    }
                    key = DeskKeys + name + (r.Figure == 1 ? "_done" : "_done_lost");
                    break;
                case SectorRules.DeskRevision:
                    Contract revision = ActiveWhere(place, SectorRules.WantsChanges);
                    subject = revision.Client;
                    r = SectorRules.Revision(revision, firm.File, SectorRules.Answer(choices, 0, 3, SectorRules.RevisionFree));
                    break;
                default:
                    r = SectorRules.Portfolio(firm.File, sf, SectorRules.Answer(choices, 0, 3, SectorRules.PortfolioPolish), seed, day, strength);
                    key = DeskKeys + name + (r.Story == SectorRules.DeskStoryAward ? "_done_award" : "_done");
                    break;
            }
            DeskMoney(kind, place, firm, r);
            DeskNews(kind, place, firm, subject, r);
            pairs = new[] { "business", business ?? string.Empty, "subject", subject, "figure", r.Figure.ToString(SectorInv), "cost", TechText.Cents(r.Cost * Prices.Level / 1000) };
            ChangedTech();
            return true;
        }

        // What a session's answer cost or brought, booked at the business (a data centre's at the player's first data centre).
        private void DeskMoney(int kind, string place, FirmPlace firm, DeskResult r)
        {
            int level = Prices.Level;
            BuildingRegistration at = firm != null ? firm.Reg : TalentGame.Find(place);
            string who = firm != null ? firm.File.Name : _infra != null && _infra.Cloud.Open ? _infra.Cloud.Name : string.Empty;
            if (r.Cost > 0)
            {
                FirmsGame.Cost(r.Cost * level / 1000, DeskCostsKey, at, who, null);
            }
            if (r.Income > 0 && firm != null)
            {
                FirmsGame.Takings(r.Income * level / 1000, firm.Reg, firm.File.Name, null);
            }
            if (r.Fine > 0 && firm != null)
            {
                FirmsGame.Fine(r.Fine * level / 1000, firm.Reg, firm.File.Name);
            }
        }

        // A session's news: the paper, the stock market, Flare.
        private void DeskNews(int kind, string place, FirmPlace firm, string subject, DeskResult r)
        {
            string firmName = firm != null ? firm.File.Name : string.Empty;
            switch (r.Story)
            {
                case SectorRules.DeskStoryBenchmark:
                    RivalProduct beaten = r.Rival >= 0 && r.Rival < _sector.Rivals.Count ? _sector.Rivals[r.Rival] : null;
                    if (beaten != null)
                    {
                        Story("reallife_tech_mkt_news_benchmark", "product", subject, "rival", beaten.Name);
                        int stock = beaten.Startups ? 0 : CompetitorRules.CompanyStock[TalentCatalogue.Clamp(beaten.Company, 0, CompetitorRules.CompanyStock.Length - 1)];
                        if (stock > 0)
                        {
                            TechBridges.Stock?.Invoke(stock, -30);
                        }
                        int own = TechBridges.OwnStockNow();
                        if (own > 0)
                        {
                            TechBridges.Stock?.Invoke(own, 30);
                        }
                        TechBridges.Fame?.Invoke(0, 1);
                    }
                    SectorBridges.Say(SectorNews.Benchmark, place, subject, r.Figure);
                    break;
                case SectorRules.DeskStoryAward:
                    Story("reallife_tech_mkt_news_award", "firm", firmName, "name", firmName);
                    TechBridges.Fame?.Invoke(0, 1);
                    SectorBridges.Say(SectorNews.Award, place, firmName, 0);
                    break;
                case SectorRules.DeskStoryIncident:
                    Story("reallife_tech_mkt_news_incident", "product", subject, "firm", firmName);
                    TechBridges.Fame?.Invoke(3, -1);
                    SectorBridges.Say(SectorNews.Incident, place, subject, 0);
                    break;
                case SectorRules.DeskStoryCaught:
                    Story("reallife_tech_mkt_news_caught", "firm", firmName, "client", subject);
                    TechBridges.Fame?.Invoke(0, 1);
                    break;
            }
        }

        // Racks bought ahead at capacity planning, in the data centre with the most room, paid now as capital. False if the cash does not cover it.
        private bool BuyAhead(int racks, long cost)
        {
            DataCentre d = SectorRules.Roomiest(_infra);
            BuildingRegistration reg = d != null ? TalentGame.Find(d.Place) : null;
            if (d == null || reg == null || racks <= 0 || InfraRules.Room(d) < racks)
            {
                return false;
            }
            if (!InfraGame.Pay(cost * Prices.Level / 1000, InfraGame.RacksKey, reg, d.Name, false))
            {
                return false;
            }
            InfraRules.Buy(d, racks, false, BuyScale);
            d.Book -= racks * InfraRules.RackPrice(false, BuyScale) - cost;   // the books hold what was paid: a tenth off ahead
            return true;
        }

        // A month of the data centres' power at the usual tariff, cents at the start's prices: what fixing a spike's price is a share of.
        private long MonthPower()
        {
            long power = 0;
            for (int i = 0; _infra != null && i < _infra.Centres.Count; i++)
            {
                power += Utility(0, InfraRules.Power(_infra.Centres[i])) * 1000 / Math.Max(1, Prices.Level);
            }
            return power * FirmsCatalogue.MonthDays;
        }

        // The cloud's takings a day as yesterday sold, cents at the start's prices: what a credit after an outage is a share of.
        private long CloudDayTakings() => _infra != null ? InfraRules.Takings(_infra.Cloud, _infra.Cloud.Sold, _infra.Cloud.GpuSold) : 0;

        // ---------------------------------------------------------------- the firms' pages as data

        /// <summary>A firm's page under the sector, built when the page is drawn; null while the sector is off.</summary>
        internal StudioScreen SectorFirmScreen(FirmPlace firm, IStudioFormat fmt)
        {
            if (!_sectorOn || firm == null || Save == null)
            {
                return null;
            }
            int day = Save.Day;
            string place = firm.File.Place;
            var s = new SectorPlace
            {
                Place = place, Name = firm.File.Name, Kind = firm.File.Kind, Reputation = firm.File.Reputation, Crew = firm.Crew, Desks = TechBridges.DesksNow(),
                ProductsAll = FirmsRules.LiveCount(_firmsFile, null), Products = FirmsRules.LiveCount(_firmsFile, place)
            };
            s.Team = TeamAt(place);
            for (int i = 0; i < _firmsFile.Products.Count; i++)
            {
                Product p = _firmsFile.Products[i];
                if (p.Place != place || p.Stage >= 2 || SectorRules.MarketOf(p) < 0)
                {
                    continue;
                }
                if (FirmsRules.CanLaunch(p) && s.Ready++ == 0)
                {
                    s.ReadyName = p.Name;
                    s.ReadyId = p.Id;
                }
                s.Customers += SectorRules.Have(p);
                s.SupportNeeded += FirmsRules.SupportNeeded(p);
            }
            for (int i = 0; i < _firmsFile.Contracts.Count; i++)
            {
                Contract w = _firmsFile.Contracts[i];
                s.Contracts += w.Place == place && w.Stage == (int)WorkStage.Active ? 1 : 0;
                if (w.Place == place && w.Stage == (int)WorkStage.Offered && s.Offers++ == 0)
                {
                    s.OfferClient = w.Client;
                }
            }
            _crews.Clear();
            for (int i = 0; i < _firmsFile.Firms.Count; i++)
            {
                FirmPlace fp = FirmOf(_firmsFile.Firms[i].Place);
                _crews.Add(fp != null ? fp.Crew : default(Crew));
            }
            s.OffersMonth = SectorRules.OffersAMonth(_firmsFile, _sector, firm.File, firm.Crew, _crews, HiringGame.Cycle);
            FirmFacts facts = FactsOf(firm, -1);
            Idle idle = FirmsRules.Why(facts, false);
            s.Idle = (int)idle;
            s.IdleWords = idle != Idle.None ? TechText.IdleLine(idle, facts, false) : null;
            for (int k = 0; k < SectorRules.Desks; k++)
            {
                if (DeskFirm(k, place) == null || SectorRules.DeskFirm[k] < 0 && !HasCentres())
                {
                    continue;
                }
                TechBridges.DeskRecordAt(place, SectorRules.DeskIds[k], out int held, out bool delegated);
                bool due = held < 0 || day - held >= SectorRules.DeskEvery[k];
                s.Due.Add(new SectorDue
                {
                    Kind = k, Due = due, Days = due ? 0 : held + SectorRules.DeskEvery[k] - day, Delegated = delegated, Blocked = SectorBlocked(k, place)
                });
            }
            return SectorViews.Firm(_sector, _firmsFile, s, day, fmt);
        }

        // A firm's people by what they do, those not leaving.
        private SectorTeam TeamAt(string place)
        {
            var t = new SectorTeam();
            for (int i = 0; i < _people.Count; i++)
            {
                Profile p = _people[i].File;
                if (p.Place != place || p.NoticeEnd != 0)
                {
                    continue;
                }
                RoleDef r = p.Def;
                t.People++;
                t.Seniors += p.Level >= TalentCatalogue.Senior ? 1 : 0;
                if (r.Id == TalentCatalogue.MlEngineer || r.Id == TalentCatalogue.ResearchScientist || r.Id == TalentCatalogue.DataScientist)
                {
                    t.Ml++;
                }
                else if (r.Id == TalentCatalogue.QaTester)
                {
                    t.Testers++;
                }
                else if (FirmsRules.Builds(r))
                {
                    t.Builders++;
                }
                t.Reliability += r.Id == TalentCatalogue.Sre || r.Id == TalentCatalogue.DevOps ? 1 : 0;
                t.Security += r.Family == Family.Security ? 1 : 0;
                switch (r.Family)
                {
                    case Family.Design:
                        t.Designers++;
                        t.Web += r.Id == TalentCatalogue.WebDesigner ? 1 : 0;
                        break;
                    case Family.Product: t.Product++; break;
                    case Family.Support: t.Support++; break;
                    case Family.Commercial: t.Sellers++; break;
                }
            }
            return t;
        }

        /// <summary>A product's market as a section of its page under the sector; null while the sector is off or for a game.</summary>
        internal ViewSection SectorMarketSection(Product p, IStudioFormat fmt)
        {
            if (!_sectorOn || p == null || SectorRules.MarketOf(p) < 0 || Save == null)
            {
                return null;
            }
            FirmPlace firm = FirmOf(p.Place);
            int at = _firmsFile.Products.IndexOf(p);
            int appeal = at >= 0 && at < _appeals.Length && _appeals[at] > 0 ? _appeals[at] : firm != null ? SectorRules.Appeal(p, firm.File, firm.Crew, _sector.Markets[SectorRules.MarketOf(p)]) : 0;
            int aware = at >= 0 && at < _aware.Length && _aware[at] > 0 ? _aware[at] : 0;
            return SectorViews.Market(_sector, p, appeal, aware, fmt);
        }

        /// <summary>The data centres' markets as a section of the Infra tab under the sector; null while the sector is off.</summary>
        internal ViewSection SectorInfraSection(IStudioFormat fmt)
        {
            return _sectorOn && _infra != null && Save != null ? SectorViews.Infra(_sector, _infra, HiringGame.Cycle, ScaleNow(), Save.Day, fmt) : null;
        }

        /// <summary>A firm's page's button: the Roles app, a launch, the way to the desk, the schedule, the contracts. False when nothing was done.</summary>
        internal bool SectorDo(int act, string place, int arg)
        {
            FirmPlace firm = FirmOf(place);
            switch (act)
            {
                case SectorAct.OpenRoles:
                    OpenRoles(TabTeam, null);
                    return true;
                case SectorAct.Launch:
                    return Launch(arg);
                case SectorAct.Desk:
                    if (firm == null)
                    {
                        return false;
                    }
                    bool marked = FirmsGame.Where(place, out string street, out int number, out _) && FirmsGame.Destination(street, number);
                    Notifications.Show(NotificationType.Info, marked ? "reallife_tech_mkt_note_desk" : "reallife_tech_mkt_note_desk_here", Data("firm", firm.File.Name), 10f);
                    return true;
                case SectorAct.Schedule:
                    return OpenRota(firm) || OpenSchedule(firm);
                default:
                    return false;
            }
        }
    }
}
