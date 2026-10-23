using System;
using System.Collections.Generic;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul.Construction
{
    /// <summary>
    /// The director's part for the city's other builders: the city's developers putting up buildings on the district's lots
    /// (DevelopmentRules: the same buildings on the same days on every machine, the host's sites shown going up), the
    /// tenders for the city's own works, for a business's building and for a public building, and their award between the
    /// player's bid and the rival builders'. On the day's pass, a site's start and the hour a tender closes.
    /// </summary>
    public sealed partial class ConstructionDirector
    {
        private readonly List<int> _devOrder = new List<int>(256);
        private bool _devOrderStale = true;
        private readonly TenderBid[] _bids = new TenderBid[BuildCatalogue.RivalCount + 1];

        private void MarketDay(int day)
        {
            Developments(day);
            if (!GoingPublic.IsGuest)
            {
                ClientTenders(day);
            }
            if (!GoingPublic.InSession)
            {
                PublicTenders(day);
            }
            RivalsDay(day);
        }

        // ------------------------------------------------------------------ the city's developers

        private void Developments(int day)
        {
            if (!Settings.ConstructionDevelopers || !DistrictLives())
            {
                return;
            }
            IReadOnlyList<LotView> lots = Lots;
            if (_devOrderStale)
            {
                DevelopmentRules.Order(_lots, _devOrder);
                _devOrderStale = false;
            }
            bool host = !GoingPublic.IsGuest;
            int started = DevelopmentRules.Started(day), shown = 0;
            for (int i = 0; i < _state.Developments.Count; i++)
            {
                shown += _state.Developments[i].SiteId > 0 && !_state.Developments[i].Handed ? 1 : 0;
            }
            for (int k = 0; k < started && k < _devOrder.Count; k++)
            {
                LotView lot = lots[_devOrder[k]];
                if (!lot.Open)
                {
                    continue;
                }
                DevFile dev = _state.Development(lot.Id);
                if (dev != null && dev.Handed)
                {
                    continue;
                }
                if (dev == null)
                {
                    if (_state.Lot(lot.Id) != null || _state.OnLot(lot.Id) != null || LotTaken(lot.Id))
                    {
                        continue;   // the player's land, or a building of the district's own: passed over
                    }
                    dev = new DevFile { Lot = lot.Id };
                    _state.Developments.Add(dev);
                }
                BuildUse use = DevelopmentRules.Use(lot.Id, lot.Class);
                int state = DevelopmentRules.State(k, lot.Id, day);
                if (string.IsNullOrEmpty(dev.Design) && !DevDesign(lot, use, dev))
                {
                    continue;   // the city's designs are being looked through: the next day's pass
                }
                if (dev.Design == NoDesign)
                {
                    continue;   // nothing of the city's fits this lot
                }
                if (state == 1 && host && dev.SiteId == 0 && shown < BuildCatalogue.DevSitesMost && SiteCount() < BuildCatalogue.SitesMost)
                {
                    StartDevelopment(dev, lot, use, k, day);
                    shown += dev.SiteId > 0 ? 1 : 0;
                }
                else if (state == 2)
                {
                    FinishDevelopment(dev, lot, use);
                }
            }
        }

        private const string NoDesign = "-";

        // The design a development wears: one of those that fit its plot's frame, picked by its lot (the same on every machine).
        private bool DevDesign(LotView lot, BuildUse use, DevFile dev)
        {
            PlotFrame(lot.Width, lot.Depth, out float w, out float d);
            if (!ReadDesigns(w, d, BuildCatalogue.UseTypes[(int)use], DesignsAsked, _choices))
            {
                return false;
            }
            KeepSizes(lot.Id, _choices);
            int pick = DevelopmentRules.DesignPick(lot.Id, _choices.Count);
            dev.Design = pick >= 0 ? _choices[pick].Name : NoDesign;
            if (pick < 0)
            {
                Logger?.Info("Construction: no design of the city's own fits lot " + lot.Id + " (" + lot.Width + " by " + lot.Depth + " m) for " + use + "; the developers pass it over.");
            }
            return true;
        }

        private bool DesignOf(LotView lot, BuildUse use, string name, out DesignChoice design)
        {
            design = default(DesignChoice);
            PlotFrame(lot.Width, lot.Depth, out float w, out float d);
            if (!ReadDesigns(w, d, BuildCatalogue.UseTypes[(int)use], DesignsAsked, _choices))
            {
                return false;
            }
            KeepSizes(lot.Id, _choices);
            for (int i = 0; i < _choices.Count; i++)
            {
                if (_choices[i].Name == name)
                {
                    design = _choices[i];
                    return true;
                }
            }
            return false;
        }

        private void StartDevelopment(DevFile dev, LotView lot, BuildUse use, int turn, int day)
        {
            int days = Math.Max(1, DevelopmentRules.Start(turn) + DevelopmentRules.Days(lot.Id) - day);
            int hours = Math.Max(10, days * 50 / 7);   // the working hours its days hold, at ten a working day
            int storeys = 4;
            if (DesignOf(lot, use, dev.Design, out DesignChoice design))
            {
                storeys = LandRules.FrameStoreys(lot.Width, PlansRules.Storeys(design.Height));
            }
            int rival = DevelopmentRules.Rival(lot.Id, use);
            dev.SiteId = StartPlotSite(new Vector3(lot.X, lot.Y, lot.Z), lot.Yaw, lot.Width, lot.Depth, storeys, rival, hours, out string why);
            if (dev.SiteId > 0)
            {
                Logger?.Info("Construction: the city's developers (" + rival + ") began on lot " + lot.Id + ": " + use + " in " + dev.Design + ", " + hours + " working hours.");
            }
            else
            {
                Logger?.Info("Construction: the developers' site on lot " + lot.Id + " was not started (" + why + "); its building is still finished on its day.");
            }
        }

        // A development's last day: its building handed to the district (the same call on every machine), its site handed over.
        private void FinishDevelopment(DevFile dev, LotView lot, BuildUse use)
        {
            if (!DesignOf(lot, use, dev.Design, out DesignChoice design))
            {
                return;   // the designs are being looked through: the next day's pass
            }
            if (!FinishLot(lot.Id, design.Name, design.Size, design.Version, BuildCatalogue.UseTypes[(int)use], 0, false, 0.0, out string why))
            {
                Logger?.Info("Construction: the developers' building on lot " + lot.Id + " is not the district's yet (" + why + ").");
                return;
            }
            dev.Handed = true;
            int rival = DevelopmentRules.Rival(lot.Id, use);
            _state.Rivals[rival].Built++;
            if (!dev.Public)
            {
                RivalNews(rival, RivalDeveloped, 1);   // the district grew: its builder, and a little every listed builder
            }
            if (dev.SiteId > 0)
            {
                FinishSite(dev.SiteId);   // the frame comes down and the building's look stands, out of view
                dev.SiteId = 0;
            }
            Logger?.Info("Construction: the city's developers (" + rival + ") finished lot " + lot.Id + " (" + use + ", " + design.Name + ").");
        }

        // ------------------------------------------------------------------ tenders

        /// <summary>The city's programme started one of its own sites: put out to tender while the player's company may bid.</summary>
        public void SiteStarted(SiteFacts site)
        {
            if (!Running || GoingPublic.IsGuest || _state.Firm.Founded <= 0 || site.Id <= 0 || site.Kind > 4)
            {
                return;   // no company, or a guest of a Going Public session: the city's contractor gets on with it
            }
            int now = ConstructionGame.Day * 24 + ConstructionGame.Hour;
            var t = new TenderFile
            {
                Id = _state.NewId(), Source = TenderSource.CityWorks, SiteId = site.Id, CivicKind = site.Kind, Street = site.Street ?? string.Empty, Number = site.Number,
                Hours = Mathf.Max(1, Mathf.RoundToInt(site.Hours)), Opened = now, Closes = now + BuildCatalogue.TenderHours
            };
            t.Value = TenderRules.WorksValue(t.CivicKind, t.Hours, ConstructionGame.Prices);
            AddTender(t);
            Tell(NotificationType.Info, "reallife_bld_notice_tender", TenderName(t), null, TabTenders, t.Id);
        }

        private void ClientTenders(int day)
        {
            if (day < _state.NextClientDay || !Settings.ConstructionDevelopers)
            {
                return;
            }
            _state.NextClientDay = day + TenderRules.NextIn(_state.Seed, day, TenderSource.Client);
            BuildRandom random = BuildRandom.Of(_state.Seed, day, 227);
            if (!ConstructionGame.Client(random.Range(0, 100000), out BuildingView b))
            {
                return;
            }
            int use = BuildCatalogue.UseOf(b.Type);
            var t = new TenderFile
            {
                Id = _state.NewId(), Source = TenderSource.Client, CivicKind = 4, Street = b.Street, Number = b.Number, Use = use >= 0 ? (BuildUse)use : BuildUse.Shops,
                Area = Math.Max(1, b.TotalSqm), Client = random.Range(0, BuildCatalogue.ClientKeys.Length), Opened = day * 24 + 8
            };
            int storeys = Math.Max(1, t.Area / 300);
            t.Hours = PlansRules.Hours(t.Use, t.Area, storeys, ProjectKind.Rebuild);
            Exchange(_exchange);
            t.Value = TenderRules.BuildingValue(PlansRules.Estimate(ProjectKind.Rebuild, t.Use, t.Area, storeys, false, 1, ConstructionGame.Prices, _exchange).Total);
            t.Closes = t.Opened + BuildCatalogue.TenderHours + 24;
            AddTender(t);
            if (_state.Firm.Founded > 0)
            {
                Tell(NotificationType.Info, "reallife_bld_notice_tender", TenderName(t), null, TabTenders, t.Id);
            }
        }

        private void PublicTenders(int day)
        {
            if (day < _state.NextPublicDay || !Settings.ConstructionDevelopers || !DistrictLives())
            {
                return;
            }
            _state.NextPublicDay = day + TenderRules.NextIn(_state.Seed, day, TenderSource.Public);
            IReadOnlyList<LotView> lots = Lots;
            BuildRandom random = BuildRandom.Of(_state.Seed, day, 229);
            int start = random.Range(0, Math.Max(1, lots.Count));
            for (int k = 0; k < lots.Count; k++)
            {
                LotView lot = lots[(start + k) % lots.Count];
                if (!lot.Open || lot.Class != 1 || DevelopmentRules.Theirs(lot.Id) || _state.Lot(lot.Id) != null || _state.OnLot(lot.Id) != null || _state.Development(lot.Id) != null
                    || LotTaken(lot.Id) || TenderOn(lot.Id))
                {
                    continue;   // an office lot no one else has: the city builds its offices there
                }
                int area = LandRules.UsualArea(lot.Width, lot.Depth, lot.Class);
                int storeys = Math.Max(1, area / Math.Max(1, lot.Width * lot.Depth / 2));
                var t = new TenderFile
                {
                    Id = _state.NewId(), Source = TenderSource.Public, CivicKind = 5, Lot = lot.Id, Street = lot.Street, Number = lot.Number, Use = BuildUse.Offices, Area = area,
                    Hours = PlansRules.Hours(BuildUse.Offices, area, storeys, ProjectKind.Plot), Client = random.Range(0, BuildCatalogue.PublicKeys.Length), Opened = day * 24 + 8
                };
                Exchange(_exchange);
                t.Value = TenderRules.BuildingValue(PlansRules.Estimate(ProjectKind.Plot, BuildUse.Offices, area, storeys, true, 2, ConstructionGame.Prices, _exchange).Total);
                t.Closes = t.Opened + BuildCatalogue.TenderHours + 48;
                AddTender(t);
                if (_state.Firm.Founded > 0)
                {
                    Tell(NotificationType.Info, "reallife_bld_notice_tender", TenderName(t), null, TabTenders, t.Id);
                }
                return;
            }
        }

        private bool TenderOn(string lot)
        {
            for (int i = 0; i < _state.Tenders.Count; i++)
            {
                if (_state.Tenders[i].Lot == lot && _state.Tenders[i].State == TenderState.Open)
                {
                    return true;
                }
            }
            return false;
        }

        private void AddTender(TenderFile t)
        {
            while (_state.Tenders.Count >= BuildCatalogue.TendersKept)
            {
                int oldest = -1;
                for (int i = 0; i < _state.Tenders.Count && oldest < 0; i++)
                {
                    oldest = _state.Tenders[i].State != TenderState.Open ? i : -1;
                }
                _state.Tenders.RemoveAt(oldest >= 0 ? oldest : 0);
            }
            _state.Tenders.Add(t);
            Logger?.Info("Construction: tender " + t.Id + " (" + t.Source + ", kind " + t.CivicKind + ") opened for " + t.Value + ", closing at hour " + t.Closes + ".");
        }

        /// <summary>A tender in words: what the works are and where.</summary>
        public string TenderName(TenderFile t)
        {
            string where = !string.IsNullOrEmpty(t.Street) ? ConstructionGame.AddressText(t.Street, t.Number) : ConstructionText.T("reallife_bld_place_lot");
            switch (t.Source)
            {
                case TenderSource.Client:
                    return ConstructionText.T("reallife_bld_tender_client", "client", ConstructionText.T(BuildCatalogue.ClientKeys[t.Client % BuildCatalogue.ClientKeys.Length]), "address", where);
                case TenderSource.Public:
                    return ConstructionText.T("reallife_bld_tender_public", "building", ConstructionText.T(BuildCatalogue.PublicKeys[t.Client % BuildCatalogue.PublicKeys.Length]), "address", where);
                default:
                    return ConstructionText.T("reallife_bld_tender_works", "works", ConstructionText.T(WorksKey(t.CivicKind)), "address", where);
            }
        }

        private static string WorksKey(int kind)
        {
            switch (kind)
            {
                case 0: return "reallife_bld_works_roadworks";
                case 1: return "reallife_bld_works_scaffold";
                case 2: return "reallife_bld_works_refit";
                case 3: return "reallife_bld_works_utility";
                default: return "reallife_bld_works_rebuild";
            }
        }

        /// <summary>The player's bid on an open tender: a share of its value and the usual or a fast time. Returns a text key.</summary>
        public string Bid(TenderFile t, int permille, bool fast)
        {
            if (t == null || t.State != TenderState.Open)
            {
                return "reallife_bld_answer_tender_closed";
            }
            if (!Licensed)
            {
                return "reallife_bld_answer_no_licence";
            }
            t.BidPermille = BuildCatalogue.Clamp(permille, BuildCatalogue.BidLeastPermille, BuildCatalogue.BidMostPermille);
            t.Fast = fast;
            Changed();
            return "reallife_bld_answer_bid";
        }

        public void Withdraw(TenderFile t)
        {
            if (t != null && t.State == TenderState.Open)
            {
                t.BidPermille = 0;
                Changed();
            }
        }

        private void TendersHour(int day, int hour)
        {
            int now = day * 24 + hour;
            for (int i = 0; i < _state.Tenders.Count; i++)
            {
                TenderFile t = _state.Tenders[i];
                if (t.State == TenderState.Open && now >= t.Closes)
                {
                    Award(t, day);
                }
            }
        }

        // The bids opened: every rival's and the player's, the best score wins.
        private void Award(TenderFile t, int day)
        {
            if (t.Source == TenderSource.CityWorks && !SiteOf(t.SiteId).Known)
            {
                t.State = TenderState.Gone;   // the city's site was finished or taken away meanwhile: nothing left to award
                Logger?.Info("Construction: tender " + t.Id + " lapsed, its site " + t.SiteId + " no longer in the programme.");
                return;
            }
            int days = TenderRules.StandardDays(t.Hours);
            Speciality need = TenderRules.NeedOf(t.Source, t.CivicKind, t.Use);
            int count = 0;
            for (int r = 0; r < BuildCatalogue.RivalCount; r++)
            {
                _bids[count++] = TenderRules.RivalBid(_state.Seed, t.Id, r, t.Value, days, need, Busy(r));
            }
            bool bid = t.BidPermille > 0 && Licensed;
            if (bid)
            {
                bool special = FirmRules.Special(_state.Firm.Speciality, t.Source == TenderSource.Public ? ProjectKind.Plot : t.CivicKind == 4
                    ? ProjectKind.Rebuild : ProjectKind.Contract, t.Use, t.CivicKind);
                _bids[count++] = TenderRules.PlayerBid(t.Value, t.BidPermille, t.Fast, days, _state.Firm.Reputation, special);
            }
            int best = TenderRules.Winner(_bids, count);
            TenderBid won = _bids[best];
            t.Winner = won.Bidder;
            t.WinningPrice = won.Price;
            t.WinningDays = won.Days;
            if (Verbose)
            {
                Logger?.Info("Construction check (tender " + t.Id + "): " + count + " bids for " + t.Value + ", the best " + won.Bidder + " at "
                    + won.Price + " in " + won.Days + " days (score " + won.Score + ")"
                             + (bid ? ", the player's " + t.BidPermille + " in 1000" + (t.Fast ? " fast" : string.Empty) : ", no bid of the player's") + ": PASS.");
            }
            RivalNews(won.Bidder, RivalTenderWon, WeightOf(t));   // the stock market hears who won: the listed builders that lost move too
            if (won.Bidder < 0)
            {
                t.State = TenderState.Won;
                _state.Firm.TendersWon++;
                _state.Firm.Reputation = FirmRules.Moved(_state.Firm.Reputation, BuildCatalogue.RepTenderWon);
                FromTender(t, day);
                Tell(NotificationType.Success, "reallife_bld_notice_tender_won", TenderName(t), null, TabProjects, t.Project);
                return;
            }
            t.State = bid ? TenderState.Lost : TenderState.Gone;
            if (bid)
            {
                _state.Firm.TendersLost++;
                Tell(NotificationType.Info, "reallife_bld_notice_tender_lost", TenderName(t), BuildCatalogue.RivalKeys[won.Bidder], TabTenders, t.Id);
            }
            _state.Rivals[won.Bidder].Won++;
            RivalWorks(t, won);
        }

        // A rival's win shows: the city's site is theirs, a business's front is scaffolded, a public building goes up on its lot.
        private void RivalWorks(TenderFile t, TenderBid won)
        {
            switch (t.Source)
            {
                case TenderSource.CityWorks:
                    SetContractor(t.SiteId, won.Bidder);
                    break;
                case TenderSource.Client:
                    t.SiteId = StartWorksSite(t.Street, t.Number, 1, false, won.Bidder, Math.Max(20, t.Hours / 2), out _);   // kept: its handover is the rival's news
                    break;
                case TenderSource.Public:
                    if (Lot(t.Lot, out LotView lot) && SiteCount() < BuildCatalogue.SitesMost)
                    {
                        var dev = new DevFile { Lot = t.Lot, Public = true };
                        if (DevDesign(lot, BuildUse.Offices, dev) && dev.Design != NoDesign)
                        {
                            dev.SiteId = StartPlotSite(new Vector3(lot.X, lot.Y, lot.Z), lot.Yaw, lot.Width, lot.Depth, LandRules.FrameStoreys(lot.Width, 6), won.Bidder, t.Hours, out _);
                            if (dev.SiteId > 0)
                            {
                                _state.Developments.Add(dev);
                                t.SiteId = dev.SiteId;   // its handover is the rival's news
                            }
                        }
                    }
                    break;
            }
        }

        // The player's win becomes a project: the city's works as they stand, a business's rebuild or a public building to start.
        private void FromTender(TenderFile t, int day)
        {
            var p = new ProjectFile
            {
                Id = _state.NewId(), Kind = ProjectKind.Contract, Source = t.Source, CivicKind = t.CivicKind, Created = day, Street = t.Street, Number = t.Number, Lot = t.Lot,
                Use = t.Use, Area = Math.Max(1, t.Area), Hours = t.Hours, ClientValue = t.WinningPrice, Client = t.Client, Tender = t.Id, Quality = 70, PlansDone = true, Engineered = true,
                Planning = PlanningState.Granted, Storeys = Math.Max(1, t.Area / 300)
            };
            if (t.Source == TenderSource.CityWorks)
            {
                p.Stage = ProjectStage.Building;
                p.SiteId = t.SiteId;
                p.StartDay = day;
                p.DueDay = PlanningRules.AfterWorkingDays(day, Math.Max(1, t.WinningDays));
                p.StageSeen = -1;
                if (ConstructionGame.Building(t.Street, t.Number, out BuildingView b))
                {
                    p.District = b.District;
                }
            }
            else
            {
                p.Stage = ProjectStage.Ready;
                if (t.Source == TenderSource.Public && Lot(t.Lot, out LotView lot))
                {
                    p.Width = lot.Width;
                    p.Depth = lot.Depth;
                    DevFile dev = new DevFile { Lot = t.Lot };
                    if (DevDesign(lot, BuildUse.Offices, dev) && dev.Design != NoDesign && DesignOf(lot, BuildUse.Offices, dev.Design, out DesignChoice design))
                    {
                        Wear(p, design);
                    }
                }
                else if (ConstructionGame.Building(t.Street, t.Number, out BuildingView b))
                {
                    p.District = b.District;
                    p.Size = b.Size;
                    p.Version = b.Version;
                }
                p.PlansDone = true;
                p.Estimate = t.Value * 1000L / (1000L + BuildCatalogue.PublicMarginPermille);
            }
            t.Project = p.Id;
            _state.Projects.Add(p);
            Logger?.Info("Construction: tender " + t.Id + " won for " + t.WinningPrice + ": project " + p.Id + (p.Stage == ProjectStage.Building
                ? " on the city's site " + p.SiteId : " to start") + ".");
        }

        // The stock market's kinds of builder news (Markets.BuilderNewsKind's numbers), and a tender's weight by its works.
        private const int RivalTenderWon = 1, RivalFinished = 2, RivalLate = 3, RivalDeveloped = 4;

        private static int WeightOf(TenderFile t)
        {
            return t.Source == TenderSource.Public ? 3 : t.Source == TenderSource.Client ? 2 : 1;
        }

        // A rival's site from a tender handed over: on time or late against the days it bid, news for the stock market.
        private void RivalHanded(int siteId, int day)
        {
            for (int i = 0; siteId > 0 && i < _state.Tenders.Count; i++)
            {
                TenderFile t = _state.Tenders[i];
                if (t.SiteId != siteId || t.Winner < 0 || t.State == TenderState.Open || t.State == TenderState.Won)
                {
                    continue;
                }
                int due = PlanningRules.AfterWorkingDays(Math.Max(0, t.Closes) / 24, Math.Max(1, t.WinningDays));
                RivalNews(t.Winner, day <= due ? RivalFinished : RivalLate, WeightOf(t));
                Logger?.Info("Construction: " + ConstructionText.T(BuildCatalogue.RivalKeys[t.Winner]) + " handed over tender " + t.Id + "'s works on day " + day + " (due " + due + ").");
                return;
            }
        }

        // How busy a rival is: the city's sites with them as the contractor, and their developments going up.
        private int Busy(int rival)
        {
            int busy = 0;
            for (int i = 0; i < _state.Tenders.Count; i++)
            {
                busy += _state.Tenders[i].State == TenderState.Lost || _state.Tenders[i].State == TenderState.Gone ? (_state.Tenders[i].Winner == rival ? 1 : 0) : 0;
            }
            return Math.Min(4, busy / 2);
        }

        // A rival's standing wanders a little, towards where it began, by the day: the same on every machine.
        private void RivalsDay(int day)
        {
            for (int r = 0; r < BuildCatalogue.RivalCount; r++)
            {
                RivalFile rival = _state.Rivals[r];
                BuildRandom random = BuildRandom.Of(r, day, 233);
                int toward = BuildCatalogue.RivalReputation[r] + Math.Min(10, rival.Built + rival.Won / 3);
                int step = rival.Reputation < toward ? 1 : rival.Reputation > toward ? -1 : 0;
                if (random.Chance(250))
                {
                    rival.Reputation = BuildCatalogue.Clamp(rival.Reputation + step, 0, 100);
                }
            }
        }
    }
}
