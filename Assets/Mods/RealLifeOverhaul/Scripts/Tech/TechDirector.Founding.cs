using System;
using System.Collections.Generic;
using Helpers;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul.Tech
{
    /// <summary>What the player picked on the page that founds a startup.</summary>
    public struct FoundDraft
    {
        public string Place;
        public int Sector, Niche, Articles, Source;
        public int Cofounder;       // an index into <see cref="TechDirector.Cofounders"/>; -1 none
        public int Split;           // thousandths of the founders' shares the co-founder gets
        public int Step;            // friends and family: which cheque
        public int Angel;           // an angel group's id
    }

    /// <summary>
    /// FoundedFirm a startup (the plan's section 7): the problem and niche, co-founders from the population or the hiring pool, a split with vesting, the pre-seed source and the
    /// incorporation, then going to market (the stages and fit) and the niches sized from the city's businesses. COST: the city's businesses are counted once a game day and kept;
    /// the product's niche view does a few comparisons for a product that has a founding that counts and nothing for the rest; nothing runs per frame.
    /// </summary>
    public sealed partial class TechDirector
    {
        internal const string FeeKey = "reallife_transaction_tech_founding_fees", ExpandKey = "reallife_transaction_tech_founding_expand", ResellKey = "reallife_transaction_tech_founding_reseller";

        private FoundedFile _founding;
        private readonly NicheCensus _census = new NicheCensus();
        private readonly NicheFacts[] _niches = new NicheFacts[NicheCatalogue.Count];     // not-AI buyers and ceilings, as the app shows them
        private readonly long[] _nicheSoft = new long[NicheCatalogue.Count], _nicheAi = new long[NicheCatalogue.Count];   // buyers today for a software product and an AI one
        private int _nicheDay = -1;
        private readonly SectorMarketDay _nicheMd = new SectorMarketDay();
        private int _servedNow;   // one bit per niche the product NicheView last weighed sells into; 0 when it sells into none
        private readonly List<CofounderOffer> _cofoundOffers = new List<CofounderOffer>(8);
        private int _cofoundDay = -1;

        /// <summary>The startups the player founded; null while the venture part is off.</summary>
        public FoundedFile Foundings => _ventureOn ? _founding : null;

        internal FoundedFirm FoundingOf(string place) => _ventureOn && _founding != null ? _founding.Of(place) : null;

        private void StartFounding()
        {
            FoundedFile file = FoundedStore.Load(Save.modData, out int version);
            _founding = file ?? new FoundedFile();
            _nicheDay = -1;
            _cofoundDay = -1;
            FoundBridges.SessionsNow(true);
            Logger?.Info("Tech: founding " + (file == null ? "opened for this save" : "loaded (version " + version + ")") + ": " + _founding.Items.Count + " startup(s) founded or placed.");
        }

        private void StopFounding()
        {
            FoundBridges.SessionsNow(false);
            _founding = null;
        }

        private void WriteFounding(Dictionary<string, string> data)
        {
            if (_founding != null && data != null)
            {
                data[FoundedStore.Key] = FoundedStore.Write(_founding);
            }
        }

        // ---------------------------------------------------------------- the day

        private void FoundingDay(int day)
        {
            if (_founding == null)
            {
                return;
            }
            FollowFounded();
            ReadNiches(day);
            for (int i = 0; i < _firms.Count; i++)
            {
                PlaceFirm(_firms[i], day);
            }
            for (int i = _founding.Items.Count - 1; i >= 0; i--)
            {
                FoundedFirm f = _founding.Items[i];
                FirmPlace firm = FirmOf(f.Place);
                Cap c = _venture.CapOf(f.Place);
                if (firm == null)
                {
                    if (c == null && day - f.Day > 2)
                    {
                        _founding.Items.RemoveAt(i);   // the firm is gone and its cap table wound down
                    }
                    continue;
                }
                CofoundersDay(f, firm, c, day);
                Mentoring(f, day);
                DemoDay(f, firm, c, day);
                GoToMarketDay(f, firm, day);
            }
        }

        // A firm that moved takes its record with it: the people part keeps the moves, which the founding follows by the same place keys.
        private void FollowFounded()
        {
            if (_talent == null)
            {
                return;
            }
            for (int m = 0; m < _talent.Moves.Count; m++)
            {
                MoveRecord move = _talent.Moves[m];
                if (!move.Whole)
                {
                    continue;
                }
                FoundedFirm f = _founding.Of(move.From);
                if (f != null && _founding.Of(move.To) == null)
                {
                    f.Place = move.To;
                }
            }
        }

        // An older save's running firm is put at the stage it already is, with a niche picked from its market, seeded. Nothing it has changes: the niche, fit and stages count
        // for it only when its player takes them up (FoundedFirm.Engaged).
        private void PlaceFirm(FirmPlace firm, int day)
        {
            Firm file = firm.File;
            if (_founding.Of(file.Place) != null || FirmsCatalogue.MakesProducts[file.Kind] == 0 || file.Kind == (int)FirmKind.Games)
            {
                return;
            }
            Cap cap = _venture.CapOf(file.Place);
            Product live = BestLive(file.Place, -1, out bool building);
            if (live == null && (cap == null || cap.Stage < 0))
            {
                return;   // a firm that has not started: it can still be founded
            }
            int stage = FoundRules.PlaceStage(live != null, building, live != null ? SectorRules.Have(live) : 0, file.Revenue30);
            int market = live != null ? SectorRules.MarketOf(live) : file.Kind == (int)FirmKind.AiLab ? (int)SectorMarket.BusinessAi : (int)SectorMarket.BusinessSoftware;
            int niche = NicheRules.OfFirm(_venture.Seed, file.Place, market);
            bool ai = file.Kind == (int)FirmKind.AiLab || (live != null && live.K == ProductKind.Model);
            int sector = ai ? (int)Venture.Ai : file.Kind == (int)FirmKind.Security ? (int)Venture.Security : (int)Venture.Software;
            FoundedFirm f = FoundRules.Place(file.Place, day, sector, niche, stage, stage >= FoundCatalogue.StageLaunch ? day : 0);
            f.Users = live != null ? (int)Math.Min(int.MaxValue, SectorRules.Have(live)) : 0;
            _founding.Items.Add(f);
            Logger?.Info("Tech: " + file.Name + " was put at the stage '" + FoundCatalogue.StageKey(stage) + "' with the niche " + niche + ": nothing it has changed.");
        }

        // The firm's product on sale with the most customers (in a market a niche serves, if one is asked), and whether another is being made.
        private Product BestLive(string place, int niche, out bool building)
        {
            building = false;
            Product best = null;
            for (int i = 0; i < _firmsFile.Products.Count; i++)
            {
                Product p = _firmsFile.Products[i];
                if (p.Place != place || p.K == ProductKind.Game)
                {
                    continue;
                }
                building |= p.Stage == 0;
                int market = SectorRules.MarketOf(p);
                if (!p.Live || (niche >= 0 && !NicheRules.Serves(NicheCatalogue.Def(niche), market)))
                {
                    continue;
                }
                if (best == null || SectorRules.Have(p) > SectorRules.Have(best))
                {
                    best = p;
                }
            }
            return best;
        }

        // ---------------------------------------------------------------- the niches, read on the day

        private int SectorDays(int day) => Math.Max(0, day - (_sectorOn ? _sector.FirstDay : _venture.FirstDay));

        private int SectorYear => _sectorOn ? _sector.YearDays : Year;

        /// <summary>
        /// The city's businesses counted into their niches once a day, and each niche's buyers, ceiling and rivals worked out from them: the game's own list of businesses, with
        /// the population's staff, rents and lettings where it runs. Never read in a frame.
        /// </summary>
        private void ReadNiches(int day)
        {
            if (_nicheDay == day || Save == null)
            {
                return;
            }
            _nicheDay = day;
            _census.Clear();
            List<BuildingRegistration> all = Save.BuildingRegistrations;
            for (int i = 0; all != null && i < all.Count; i++)
            {
                BuildingRegistration reg = all[i];
                if (reg == null || string.IsNullOrEmpty(reg.businessTypeName) || reg.businessTypeName == "ba:businesstype_empty" || GameDlc.Gated(reg))
                {
                    continue;
                }
                NicheRules.Count(_census, reg.businessTypeName, 0);
            }
            FoundBridges.CensusNow(_census);
            NicheRules.Exist(_census, CanHave);
            _census.Day = day;
            int days = SectorDays(day), year = SectorYear, cycle = HiringGame.Cycle, season = CalendarRules.Season(day, Year), hype = HypeNow;
            for (int n = 0; n < NicheCatalogue.Count; n++)
            {
                NicheDef d = NicheCatalogue.Defs[n];
                NicheMood soft = NicheRules.Mood(d, _census, cycle, season, hype, false), ai = NicheRules.Mood(d, _census, cycle, season, hype, true);
                _nicheSoft[n] = _census.Exists[n] ? NicheRules.Buyers(d, _census, days, year, soft) : 0;
                _nicheAi[n] = _census.Exists[n] ? NicheRules.Buyers(d, _census, days, year, ai) : 0;
                int market = NicheRules.MarketOf(d, false);
                SectorMarketDay md = _sectorOn ? _sector.Markets[market] : null;
                int going = NicheRules.Going(d, md != null ? md.Going : 1000);
                long marketBuyers = md != null ? md.Buyers : 0;
                int share = marketBuyers > 0 ? NicheRules.RivalShare(_nicheSoft[n], marketBuyers) : 1000;
                _niches[n] = new NicheFacts
                {
                    Exists = _census.Exists[n], Businesses = _census.Businesses[n], Staff = _census.Staff[n], Buyers = _nicheSoft[n], Going = going, Share = share, Mood = soft,
                    Ceiling = NicheRules.Ceiling(d, _nicheSoft[n], going), Rivals = md != null ? Math.Max(md.Rivals > 0 ? 1 : 0, (int)((long)md.Rivals * share / 1000)) : 0
                };
            }
        }

        private static bool CanHave(string type)
        {
            try
            {
                return BusinessTypeHelper.GetData(type) != null;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>A niche as the app shows it today.</summary>
        internal NicheFacts NicheNow(int niche)
        {
            if (_founding != null && Save != null && _nicheDay != Save.Day)
            {
                ReadNiches(Save.Day);
            }
            return niche >= 0 && niche < NicheCatalogue.Count ? _niches[niche] : default(NicheFacts);
        }

        /// <summary>The niche of one of the city's startups, seeded; -1 for none.</summary>
        internal int NicheOfStartup(Startup s) => _ventureOn ? NicheRules.OfStartup(_venture.Seed, s.Id, s.Sector) : -1;

        // ---------------------------------------------------------------- a product in its niche

        /// <summary>
        /// The market a product of a founded firm sells into: the buyers of its niche (and the neighbours opened) in place of the whole market's, the rivals that sell there, the
        /// price its businesses will bear and what the launch channel adds. The same market, unchanged, for every other product. The caller's appeal, awareness and crew are
        /// worked again for the niche.
        /// </summary>
        private SectorMarketDay NicheView(FirmPlace firm, Product p, SectorMarketDay md, int day, long pulled, ref int appeal, ref int aware, ref Crew crew)
        {
            _servedNow = 0;
            FoundedFirm f = _founding != null ? _founding.Of(firm.File.Place) : null;
            if (f == null || !f.Counts || f.Niche < 0)
            {
                return md;
            }
            int market = SectorRules.MarketOf(p);
            bool ai = NicheRules.IsAi(market);
            long[] today = ai ? _nicheAi : _nicheSoft;
            long total = 0;
            bool serves = false;
            int bits = 0;
            if (NicheRules.Serves(NicheCatalogue.Def(f.Niche), market))
            {
                total += today[f.Niche];
                serves = true;
                bits |= 1 << f.Niche;
            }
            for (int i = 0; i < f.Expanded.Count; i++)
            {
                if (NicheRules.Serves(NicheCatalogue.Def(f.Expanded[i].Niche), market))
                {
                    total += today[f.Expanded[i].Niche] * NicheRules.Ramp(day - f.Expanded[i].Day) / 1000;
                    serves = true;
                    bits |= 1 << f.Expanded[i].Niche;
                }
            }
            if (!serves)
            {
                return md;
            }
            long buyers = Math.Min(total, md.Buyers);
            SectorMarketDay v = _nicheMd;
            NicheRules.View(v, md, buyers, (int)Math.Max(1L, (long)NicheRules.Going(NicheCatalogue.Def(f.Niche), md.Going) * FoundRules.Bear(f) / 1000));
            buyers = v.Buyers;
            appeal = SectorRules.Appeal(p, firm.File, crew, v);
            SectorProduct sp = _sector.ProductAt(p.Id);
            int marketing = FirmsCatalogue.MarketingDay[TalentCatalogue.Clamp(p.Marketing, 0, FirmsCatalogue.MarketingDay.Length - 1)];
            aware = SectorRules.Awareness(marketing, buyers, SectorRules.Have(p), md.Who, SectorRules.Boost(sp, day) + FoundRules.Boost(f.Channel) + f.Wom);
            _servedNow = bits;
            if (RivalryOn)
            {
                // one pool of buyers for everyone who sells in the niche: the city's startups pull in the same buyers (RivalryRules.PlayerAll)
                v.All = RivalryRules.PlayerAll(_world, bits, market, md.All, SectorRules.Attraction(appeal, aware), pulled, buyers, md.Buyers, day);
            }
            else
            {
                NicheRules.Pull(v, md, SectorRules.Attraction(appeal, aware), pulled);
            }
            if (f.Channel == FoundCatalogue.ChannelSales)
            {
                crew.Selling += FoundRules.FounderPoints(TechBridges.SkillNow(TalentCatalogue.Selling), TechBridges.SkillNow(TalentCatalogue.Relationships));
            }
            f.AppealNow = appeal;
            f.SoundNow = SectorRules.Soundness(p.Bugs, FirmsRules.Allowance(p));
            f.BuyersNow = buyers;
            f.ChurnNow = FirmsCatalogue.Churn[TalentCatalogue.Clamp(p.Segment, 0, 2)];
            return v;
        }

        private static bool NicheServes(FoundedFirm f, Product p)
        {
            int market = SectorRules.MarketOf(p);
            bool serves = f.Niche >= 0 && NicheRules.Serves(NicheCatalogue.Def(f.Niche), market);
            for (int i = 0; i < f.Expanded.Count && !serves; i++)
            {
                serves = NicheRules.Serves(NicheCatalogue.Def(f.Expanded[i].Niche), market);
            }
            return serves;
        }

        // What a day of a product sold in its niche came to: customers won and lost are noted for retention, and a partner that resells keeps its cut.
        private void NicheSold(FirmPlace firm, Product p, in ProductDay r)
        {
            FoundedFirm f = _founding != null ? _founding.Of(firm.File.Place) : null;
            if (f == null || !f.Counts || f.Niche < 0)
            {
                return;
            }
            if (BestLive(firm.File.Place, f.Niche, out _) == p)
            {
                FoundRules.Note(f, r.Won, r.Lost, SectorRules.Have(p));
            }
            int cut = FoundRules.Cut(f.Channel);
            if (cut > 0 && r.Takings > 0 && NicheServes(f, p))
            {
                string partner = f.Partner >= 0 && f.Partner < HiringCatalogue.RivalFirms.Length ? HiringCatalogue.RivalFirms[f.Partner] : string.Empty;
                FirmsGame.Cost(r.Takings * cut / 1000, ResellKey, firm.Reg, partner, null);
            }
        }

        // ---------------------------------------------------------------- the day of a firm going to market

        private void GoToMarketDay(FoundedFirm f, FirmPlace firm, int day)
        {
            if (!f.Counts)
            {
                return;
            }
            Product p = BestLive(f.Place, f.Niche, out bool building);
            if (p == null && f.Niche >= 0)
            {
                p = BestLive(f.Place, -1, out building);
            }
            if (p != null && f.Stage < FoundCatalogue.StageUsers && f.Stage >= FoundCatalogue.StageBuild)
            {
                FoundRules.Next(f, FoundCatalogue.StageUsers);
            }
            var x = new FitInputs
            {
                Appeal = f.AppealNow > 0 ? f.AppealNow : 1000, Soundness = f.SoundNow > 0 ? f.SoundNow : p != null ? SectorRules.Soundness(p.Bugs, FirmsRules.Allowance(p)) : 1000,
                Reputation = firm.File.Reputation, Users = p != null ? SectorRules.Have(p) : 0, NicheBuyers = f.BuyersNow > 0 ? f.BuyersNow : f.Niche >= 0 ? Math.Max(1L, _nicheSoft[f.Niche]) : 1,
                ChurnBase = f.ChurnNow > 0 ? f.ChurnNow : FirmsCatalogue.Churn[1]
            };
            if (p != null && f.Users == 0)
            {
                f.Users = (int)Math.Min(int.MaxValue, x.Users);
            }
            bool found = FoundRules.Day(f, p != null, x, day);
            if (found)
            {
                TellTech(NotificationType.Success, "reallife_gtm_note_fit", Data("firm", firm.File.Name, "fit", TechText.Mille(f.Fit)), TabFunding, TechApp.CapRow + f.Place);
                Story("reallife_gtm_news_fit", "firm", firm.File.Name);
            }
        }

        // ---------------------------------------------------------------- the pre-seed accelerator

        private void Mentoring(FoundedFirm f, int day)
        {
            if (f.MentorUntil <= 0 || day > f.MentorUntil)
            {
                return;
            }
            for (int i = 0; i < FoundCatalogue.AccelSkills.Length; i++)
            {
                int skill = FoundCatalogue.AccelSkills[i];
                FoundBridges.LearnNow(skill, FoundCatalogue.AccelLearn * Math.Max(1, 100 - TechBridges.SkillNow(skill)) / 100);
            }
            if (day == f.MentorUntil)
            {
                TellTech(NotificationType.Info, "reallife_found_note_mentor_end", Data("firm", FirmOf(f.Place)?.File.Name ?? string.Empty), TabFunding, TechApp.CapRow + f.Place);
            }
        }

        // Demo day: venture firms that lead the seed round meet the firm and offer sheets, priced by what it has learned and how far it has come.
        private void DemoDay(FoundedFirm f, FirmPlace firm, Cap c, int day)
        {
            if (f.DemoDay <= 0 || f.DemoHeld || day < f.DemoDay || c == null)
            {
                return;
            }
            f.DemoHeld = true;
            long arr = FirmsRules.Arr(_firmsFile, f.Place);
            long value = Math.Max(VentureCatalogue.FloorValue[0] * FoundRules.FitFactor(f) / 1000, FirmsRules.Value(firm.File, arr, 500 + FoundRules.GrowthBp(f)));
            int made = 0;
            for (int k = 0; k < VentureCatalogue.FirmCount && made < FoundCatalogue.DemoSheets; k++)
            {
                int vc = (k + TechRandom.Range(_venture.Seed, day, 83, 0, 0, VentureCatalogue.FirmCount - 1)) % VentureCatalogue.FirmCount;
                VcDef def = VentureCatalogue.Firms[vc];
                if (!def.Leads || FoundCatalogue.DemoStage < def.Least || FoundCatalogue.DemoStage > def.Most || day < _venture.ClosedUntil[vc] || SheetFrom(c, vc))
                {
                    continue;
                }
                TermSheet t = FoundRules.DemoSheet(vc, value, f, VentureRules.Mood(HiringGame.Cycle), day);
                t.T = TermsFor(c, firm, vc, day);   // a demo day's lead asks what it asks at any pitch
                c.Sheets.Add(t);
                made++;
            }
            TellTech(made > 0 ? NotificationType.Success : NotificationType.Info, made > 0 ? "reallife_found_note_demo" : "reallife_found_note_demo_none",
                Data("firm", firm.File.Name, "count", made.ToString()), TabFunding, TechApp.CapRow + f.Place);
            ChangedTech();
        }

        // ---------------------------------------------------------------- co-founders

        // A co-founder whose file has left the payroll has left the firm; what they keep is what had vested.
        private void CofoundersDay(FoundedFirm f, FirmPlace firm, Cap c, int day)
        {
            for (int i = 0; i < f.Cofounders.Count; i++)
            {
                CoFounder co = f.Cofounders[i];
                if (co.Left > 0 || _talent == null || _talent.Of(co.Profile) != null)
                {
                    continue;
                }
                Left(f, co, c, firm, day);
            }
        }

        private void Left(FoundedFirm f, CoFounder co, Cap c, FirmPlace firm, int day)
        {
            long cancelled = c != null ? FoundRules.Leave(c, co, day, Year) : 0L;
            if (c == null)
            {
                co.Left = day;
            }
            TellTech(NotificationType.Warning, "reallife_found_note_cofounder_left",
                Data("firm", firm != null ? firm.File.Name : string.Empty, "name", co.Name, "kept", co.Kept.ToString("N0", System.Globalization.CultureInfo.InvariantCulture),
                    "back", cancelled.ToString("N0", System.Globalization.CultureInfo.InvariantCulture)), TabFunding, TechApp.CapRow + f.Place);
            RefreshFloor(day);
            ChangedTech();
        }

        /// <summary>The co-founder parts ways with the firm at the player's word: their shares are the vested part, the rest go back, and they are laid off.</summary>
        internal bool PartWays(string place, int no)
        {
            FoundedFirm f = FoundingOf(place);
            CoFounder co = _founding != null ? _founding.Cofounder(place, no) : null;
            FirmPlace firm = FirmOf(place);
            if (f == null || co == null || co.Left > 0 || firm == null || Save == null)
            {
                return false;
            }
            Cap c = _venture.CapOf(place);
            if (_talent != null && _talent.Of(co.Profile) != null)
            {
                LetGo(co.Profile, false);
            }
            Left(f, co, c, firm, Save.Day);
            return true;
        }

        /// <summary>
        /// People who could co-found a firm, read once a day: the city's population through its records when it runs, else professionals of the hiring pool. Never both.
        /// </summary>
        internal List<CofounderOffer> Cofounders()
        {
            if (Save == null || _cofoundDay == Save.Day)
            {
                return _cofoundOffers;
            }
            _cofoundDay = Save.Day;
            _cofoundOffers.Clear();
            var found = new List<CofounderOffer>(12);
            if (FoundBridges.PopulationRuns)
            {
                FoundBridges.PeopleNow(_venture.Seed, Save.Day, found, 12);
                for (int i = 0; i < found.Count && _cofoundOffers.Count < 4; i++)
                {
                    if (!_founding.Taken.Contains(found[i].Person))
                    {
                        _cofoundOffers.Add(found[i]);
                    }
                }
                return _cofoundOffers;
            }
            if (!_hiringOn)
            {
                return _cofoundOffers;
            }
            int start = TechRandom.Range(_venture.Seed, Save.Day, 85, 0, 0, Math.Max(0, _hiring.Seekers.Count - 1));
            for (int k = 0; k < _hiring.Seekers.Count && _cofoundOffers.Count < 4; k++)
            {
                Seeker s = _hiring.Seekers[(start + k) % _hiring.Seekers.Count];
                if (s.Taken || s.P.Level < TalentCatalogue.Mid || !Founds(s.P.Role))
                {
                    continue;
                }
                _cofoundOffers.Add(new CofounderOffer
                {
                    Origin = CofounderOrigin.Pool, Person = s.Id, Name = s.P.Name, Female = s.P.Female, Age = s.P.Age, Role = s.P.Role, Level = s.P.Level, Main = s.P.Main, A = s.P.A, B = s.P.B,
                    Years = s.P.YearsDays
                });
            }
            return _cofoundOffers;
        }

        // The roles a founder can be: the ones that build, sell, design or run a product.
        private static bool Founds(int role)
        {
            return role == TalentCatalogue.SoftwareEngineer || role == 1 || role == 2 || role == 13 || role == 14 || role == 16 || role == 17 || role == 18 || role == 23 || role == 24;
        }

        // ---------------------------------------------------------------- founding

        /// <summary>The firms of the player's that can be founded as startups: a tech business with no founding and no cap table yet.</summary>
        internal void Foundable(List<FirmPlace> into)
        {
            into.Clear();
            for (int i = 0; _founding != null && i < _firms.Count; i++)
            {
                FirmPlace firm = _firms[i];
                Cap c = _venture.CapOf(firm.File.Place);
                if (_founding.Of(firm.File.Place) == null && (c == null || c.Stage < 0) && FirmsCatalogue.MakesProducts[firm.File.Kind] != 0 && firm.File.Kind != (int)FirmKind.Games)
                {
                    into.Add(firm);
                }
            }
        }

        /// <summary>The incorporation fee at today's prices for these articles and the player's Law.</summary>
        internal long FeeNow(int articles) => Level(FoundRules.Fee(articles, TechBridges.SkillNow(TalentCatalogue.Law)));

        /// <summary>
        /// The startup founded: the legal fees booked at the firm, the cap table made with the co-founders' shares vesting from today, the first money landing in the firm account
        /// as any raise does (so it is held for the investors and counts for the floor), the co-founders on the payroll with their own skills. False, with a notice, when it
        /// cannot be: nothing is changed then.
        /// </summary>
        internal bool FoundStartup(FoundDraft d)
        {
            FirmPlace firm = FirmOf(d.Place);
            if (!_ventureOn || _founding == null || firm == null || Save == null || _founding.Of(d.Place) != null || _venture.CapOf(d.Place) != null)
            {
                Notifications.ShowError("reallife_found_why_state");
                return false;
            }
            int day = Save.Day;
            long fee = FeeNow(d.Articles);
            if (VentureGame.Cash < fee)
            {
                Notifications.Show(NotificationType.Warning, "reallife_found_why_cash", Data("fee", TechText.Cents(fee)), 8f, null, null, true, false);
                return false;
            }
            var offers = Cofounders();
            CofounderOffer offer = d.Cofounder >= 0 && d.Cofounder < offers.Count ? offers[d.Cofounder] : default(CofounderOffer);
            bool partner = d.Cofounder >= 0 && d.Cofounder < offers.Count;
            if (partner && (_talent == null || _talent.People.Count >= TalentStore.PeopleMost))
            {
                Notifications.ShowError("reallife_found_why_team");
                return false;
            }
            var co = new List<CoFounder>(1);
            if (partner)
            {
                co.Add(Hire(offer, d.Place, day));
            }
            Cap cap = FoundRules.Incorporate(d.Place, co, d.Split, day);
            _venture.Caps.Add(cap);
            long money = 0;
            switch (d.Source)
            {
                case FoundCatalogue.SourceFamily:
                    money = FoundRules.TakeFamily(cap, FoundRules.FamilyAmount(d.Step), day);
                    break;
                case FoundCatalogue.SourceAccelerator:
                    money = FoundRules.TakeAccelerator(cap, day);
                    break;
                case FoundCatalogue.SourceAngels:
                    money = TakeAngel(cap, d, firm, day);
                    break;
            }
            FoundedFirm f = FoundRules.Found(d.Place, day, d.Sector, d.Niche, d.Articles, d.Source, money, FoundRules.Fee(d.Articles, TechBridges.SkillNow(TalentCatalogue.Law)));
            f.Cofounders.AddRange(co);
            if (d.Source == FoundCatalogue.SourceAccelerator)
            {
                f.MentorUntil = f.DemoDay = day + FoundCatalogue.AccelDays;
            }
            _founding.Items.Add(f);
            if (partner && offer.Origin == CofounderOrigin.Population)
            {
                _founding.Taken.Add(offer.Person);
            }
            if (money > 0)
            {
                cap.Acc.FounderDay = day;   // the founder's own shares vest from the day the firm was founded
                cap.Acc.Name = firm.File.Name;
                VentureGame.Give(Level(money), VentureGame.RaiseKey, firm.File.Name, firm.Reg);
                RefreshFloor(day);
            }
            FirmsGame.Pay(fee, FeeKey, firm.Reg, firm.File.Name);
            Story("reallife_found_news_founded", "firm", firm.File.Name);
            TellTech(NotificationType.Success, "reallife_found_note_founded",
                Data("firm", firm.File.Name, "niche", d.Niche >= 0 ? TechText.T(NicheCatalogue.NameKey(d.Niche)) : "-", "raised", TechText.Cents(Level(money)), "fee", TechText.Cents(fee)),
                TabFunding, TechApp.CapRow + d.Place);
            Logger?.Info("Tech: " + firm.File.Name + " founded on day " + day + ": niche " + d.Niche + ", source " + d.Source + ", " + TechText.Cents(Level(money)) + " raised, fees "
                         + TechText.Cents(fee) + ".");
            ChangedTech();
            return true;
        }

        // An angel group's cheque for a founder who has not pitched: the venture rules' own sheet, taken at once.
        private long TakeAngel(Cap cap, FoundDraft d, FirmPlace firm, int day)
        {
            int vc = TalentCatalogue.Clamp(d.Angel, 0, VentureCatalogue.FirmCount - 1);
            if (!VentureCatalogue.Firms[vc].Angels || !VentureCatalogue.Firms[vc].Leads)
            {
                vc = VentureRules.AnyLead((int)RoundStage.PreSeed);
            }
            return VentureRules.Accept(cap, AngelOffer(vc, d.Articles), 0, day);
        }

        /// <summary>The cheque an angel group offers a new founder today, for the articles picked (what the founding page shows and what is taken).</summary>
        internal TermSheet AngelOffer(int vc, int articles)
        {
            var asked = new FoundedFirm { Articles = articles };
            return FoundRules.AngelSheet(vc, VentureCatalogue.FloorValue[0], TechBridges.SkillNow(TalentCatalogue.Selling), TechBridges.SkillNow(TalentCatalogue.Relationships),
                VentureRules.Mood(HiringGame.Cycle), Save != null ? Save.Day : 0, asked);
        }

        // A co-founder on the payroll: their file from the record (a person of the population) or the pool's candidate, at the firm, paid half the going rate for they hold shares.
        private CoFounder Hire(CofounderOffer o, string place, int day)
        {
            Profile p = TalentRules.Make(_venture.Seed, o.Person * 7 + day, o.Role, o.Level, day);
            p.Id = "pro:" + _talent.NextPro++;
            p.Name = o.Name;
            p.Female = o.Female;
            p.Age = o.Age;
            p.Main = o.Main;
            p.A = o.A;
            p.B = o.B;
            p.YearsDays = o.Years;
            p.Place = place;
            p.Work = (int)Arrangement.Hybrid;
            string skill = string.IsNullOrEmpty(p.Def.GameSkill) ? TalentCatalogue.RateSkill : p.Def.GameSkill;
            p.Wage = Math.Max(TalentCatalogue.RateLeast, TalentRules.Going(TalentGame.GameRate(skill, p.Main / 1000), p.Def, 1000, 0) * FoundCatalogue.CofounderPay / 1000);
            p.Joined = day;
            p.LevelDay = day;
            p.ReviewDay = TalentRules.FirstReview(day, Year);
            p.Morale = 750;
            _talent.People.Add(p);
            if (o.Origin == CofounderOrigin.Pool && _hiringOn)
            {
                Seeker s = _hiring.SeekerOf(o.Person);
                if (s != null)
                {
                    s.Taken = true;
                }
            }
            ReadPeople(day);
            return new CoFounder
            {
                No = _founding.NextNo++, Name = o.Name, Female = o.Female, Age = o.Age, Origin = o.Origin, Person = o.Person, Role = o.Role, Level = o.Level, Profile = p.Id, Day = day
            };
        }

        // ---------------------------------------------------------------- going to market, from the page

        /// <summary>A placed firm's player takes up its niche, fit and sessions: from today they count for it.</summary>
        internal void Engage(string place)
        {
            FoundedFirm f = FoundingOf(place);
            if (f != null && f.Placed && !f.Engaged)
            {
                f.Engaged = true;
                ChangedTech();
            }
        }

        /// <summary>A neighbouring niche opened: it reuses most of the product, costs days of the firm's burn, and its buyers count in more and more over two months.</summary>
        internal bool Expand(string place, int niche)
        {
            FoundedFirm f = FoundingOf(place);
            FirmPlace firm = FirmOf(place);
            if (f == null || firm == null || Save == null || !f.Counts || !FoundRules.CanExpand(f, niche) || !_census.Exists[niche])
            {
                Notifications.ShowError("reallife_gtm_why_expand");
                return false;
            }
            long burn = Math.Max(0, -FirmProfit(firm)) * 1000 / Math.Max(1, Prices.Level);
            long cost = Level(FoundRules.ExpandCost(burn));
            if (!FirmsGame.Pay(cost, ExpandKey, firm.Reg, firm.File.Name))
            {
                Notifications.ShowInsufficientMoney();
                return false;
            }
            f.Expanded.Add(new Expansion { Niche = niche, Day = Save.Day });
            TellTech(NotificationType.Success, "reallife_gtm_note_expand", Data("firm", firm.File.Name, "niche", TechText.T(NicheCatalogue.NameKey(niche)), "cost", TechText.Cents(cost)),
                TabFunding, TechApp.CapRow + place);
            ChangedTech();
            return true;
        }

        /// <summary>The cost to open a niche, at today's prices.</summary>
        internal long ExpandCostNow(string place)
        {
            FirmPlace firm = FirmOf(place);
            long burn = firm != null ? Math.Max(0, -FirmProfit(firm)) * 1000 / Math.Max(1, Prices.Level) : 0;
            return Level(FoundRules.ExpandCost(burn));
        }

        // ---------------------------------------------------------------- what the ladder, the sheets and the books ask of founding

        /// <summary>Whether the board can remove the founder of this firm (the articles; a firm not founded here keeps the ladder as it was).</summary>
        private bool FoundingCanRemove(Cap c) => FoundRules.CanRemove(_founding != null ? _founding.Of(c.Place) : null, c);

        // The board meant to remove the founder and may not: the investors cannot, so they shut their doors and the claim follows its days.
        private void Unremoved(Cap c, FirmPlace firm, int day)
        {
            ShutDoors(day);
            TechBridges.Fame?.Invoke(3, -MoneyCatalogue.StandingFall);
            TellTech(NotificationType.Error, "reallife_vc_note_removal_blocked", Data("firm", firm.File.Name, "vc", LeadName(c)), TabFunding, TechApp.CapRow + c.Place);
            Logger?.Info("Tech: the board of " + firm.File.Name + " could not remove the founder under its articles with " + TechText.Cents(Level(c.Acc.Taken)) + " taken.");
            RefreshFloor(day);
            ChangedTech();
        }

        // The founder's own shares vest from the day the firm was founded, not from the day the first investor came.
        private void FoundingAccountOpened(Cap c)
        {
            FoundedFirm f = _founding != null ? _founding.Of(c.Place) : null;
            if (f != null && !f.Placed)
            {
                c.Acc.FounderDay = f.Day;
            }
        }

        // A firm closed with friends and family on its cap table: what they got back against what they put in is the strain on the player's partner.
        private void FoundingWound(Cap c, StartupMoney.Wind w, string name)
        {
            Holder family = null;
            int at = -1;
            for (int i = 0; i < c.Holders.Count; i++)
            {
                if (c.Holders[i].Kind == (int)HolderKind.Vc && c.Holders[i].Who == VentureCatalogue.FamilyId)
                {
                    family = c.Holders[i];
                    at = i;
                }
            }
            if (family == null)
            {
                return;
            }
            long[] pay = VentureRules.Waterfall(c.Holders, w.Price);
            long got = w.Owed > 0 ? pay[at] * w.Paid / Math.Max(1L, w.Owed) : 0L;
            int loss = FoundRules.FamilyLoss(got, family.Invested);
            if (loss <= 0)
            {
                return;
            }
            FoundBridges.LetDownNow(Math.Min(100, loss * FoundCatalogue.FamilyStrain / 100), Math.Min(100, loss * FoundCatalogue.FamilyAffection / 100));
            Notifications.Show(NotificationType.Warning, "reallife_found_note_family_lost", Data("firm", name, "lost", TechText.Mille(loss)), 12f, null, null, true, false);
        }
    }
}
