using System;
using System.Collections.Generic;
using System.Globalization;
using UI.Notification;

namespace RealLifeOverhaul.Tech
{
    /// <summary>
    /// Competition in niches (Tech/Rivals, the plan's section 7), under the venture part and the sector: the city's startups sell into the niche their founding gave them from the same
    /// pool of buyers as the player's products, and react on the day (cutting prices, copying a lead, raising a war chest, hiring, being bought by a listed company; a listed company
    /// comes into a niche that got big), and big customers hold bake offs (TechDirector.BakeOff). The rules are RivalryRules'; this part reads the game into them and tells what they did.
    /// With Startups off nothing here runs. COST: the niches are read once a day with the founding's census; a rival's setup and views are a pass over the rival products inside the
    /// sector's day; nothing per frame.
    /// </summary>
    public sealed partial class TechDirector
    {
        private RivalryFile _rival;
        private RivalryWorld _world;
        private int _worldDay = -1;
        private Func<RivalProduct, bool> _keptRival;
        private readonly List<RivalryLine> _rivalLines = new List<RivalryLine>(8);

        /// <summary>What competition keeps; null while the venture part is off.</summary>
        public RivalryFile Competition => _ventureOn ? _rival : null;

        private void StartRivalry()
        {
            RivalryFile file = RivalryStore.Load(Save.modData, out int version);
            _rival = file ?? new RivalryFile();
            _world = new RivalryWorld { Rec = _rival };
            _worldDay = -1;
            _keptRival = r => RivalryRules.Kept(_rival, r);
            RivalryBridges.SessionsNow(true);
            Logger?.Info("Tech: competition in niches " + (file == null ? "opened for this save" : "loaded (version " + version + ")") + ": " + _rival.Recs.Count + " rival(s) followed, "
                         + _rival.Bakes.Count + " bake off(s); the shared pools fade in over " + RivalryCatalogue.RampDays + " days from the day it first runs.");
        }

        private void StopRivalry()
        {
            RivalryBridges.SessionsNow(false);
            ClearRivalry();
            _rival = null;
            _world = null;
        }

        private void WriteRivalry(Dictionary<string, string> data)
        {
            if (_rival != null && data != null)
            {
                data[RivalryStore.Key] = RivalryStore.Write(_rival);
            }
        }

        // The sector's rival products carry the day's niche setup; with competition off none of it is left on them.
        private void ClearRivalry()
        {
            for (int i = 0; _sector != null && i < _sector.Rivals.Count; i++)
            {
                RivalProduct r = _sector.Rivals[i];
                r.Niche = -1;
                r.Frame = null;
                r.View = null;
                r.PushMarketing = 0;
                r.PushPoints = 0;
            }
        }

        private bool RivalryOn => _rival != null && _world != null && _sectorOn && _sector != null && !SharedCity.Follows(SectorShare.Part);

        private void BindWorld()
        {
            _world.Sector = _sector;
            _world.Venture = _venture;
            _world.Rec = _rival;
        }

        // ---------------------------------------------------------------- the sector's day, as competition reads it

        /// <summary>Before the rivals' pull is worked out: the niches read, each rival's niche, frame and pushes set.</summary>
        private void RivalryPrepare(int day)
        {
            if (!RivalryOn)
            {
                ClearRivalry();
                return;
            }
            BindWorld();
            if (_rival.Since < 0)
            {
                _rival.Since = day;
            }
            if (_founding != null)
            {
                ReadNiches(day);
            }
            Array.Copy(_nicheSoft, _world.Soft, NicheCatalogue.Count);
            Array.Copy(_nicheAi, _world.Ai, NicheCatalogue.Count);
            RivalryRules.Prepare(_world, day);
        }

        /// <summary>After the player's products are counted: a product of the player's that sells into a niche is added to the pool, then the rivals' views are set.</summary>
        private void RivalryOwn(FirmPlace firm, Product p, SectorMarketDay md, int day, int index)
        {
            if (!RivalryOn || _founding == null || !p.Live)
            {
                return;
            }
            Crew crew = firm.Crew;
            int appeal = index < _appeals.Length ? _appeals[index] : 0, aware = index < _aware.Length ? _aware[index] : 0;
            long pulled = SectorRules.Attraction(appeal, aware);
            SectorMarketDay v = NicheView(firm, p, md, day, pulled, ref appeal, ref aware, ref crew);
            FoundedFirm f = _founding.Of(firm.File.Place);
            if (ReferenceEquals(v, md) || f == null || _servedNow == 0)
            {
                return;
            }
            int niche = (_servedNow & (1 << f.Niche)) != 0 ? f.Niche : LowBit(_servedNow);
            _world.Pool.AddPlayer(niche, SectorRules.MarketOf(p), SectorRules.Attraction(appeal, aware), pulled);
        }

        private static int LowBit(int bits)
        {
            for (int n = 0; n < NicheCatalogue.Count; n++)
            {
                if ((bits & (1 << n)) != 0)
                {
                    return n;
                }
            }
            return 0;
        }

        private void RivalryViews(int day)
        {
            if (RivalryOn)
            {
                RivalryRules.Views(_world, day);
            }
        }

        // ---------------------------------------------------------------- the player's firms, as competition reads them

        private void FillWorld(int day)
        {
            BindWorld();
            Array.Copy(_nicheSoft, _world.Soft, NicheCatalogue.Count);
            Array.Copy(_nicheAi, _world.Ai, NicheCatalogue.Count);
            _world.Selling = TechBridges.SkillNow(TalentCatalogue.Selling);
            _world.Sense = TechBridges.SkillNow(TalentCatalogue.ProductSense);
            _world.Relationships = TechBridges.SkillNow(TalentCatalogue.Relationships);
            _world.You.Clear();
            for (int i = 0; _founding != null && i < _founding.Items.Count; i++)
            {
                FoundedFirm f = _founding.Items[i];
                FirmPlace firm = f.Counts && f.Niche >= 0 ? FirmOf(f.Place) : null;
                Product p = firm != null ? BestLive(f.Place, f.Niche, out _) : null;
                if (p == null)
                {
                    continue;
                }
                int market = SectorRules.MarketOf(p), served = 0;
                if (NicheRules.Serves(NicheCatalogue.Def(f.Niche), market))
                {
                    served |= 1 << f.Niche;
                }
                for (int e = 0; e < f.Expanded.Count; e++)
                {
                    served |= NicheRules.Serves(NicheCatalogue.Def(f.Expanded[e].Niche), market) ? 1 << f.Expanded[e].Niche : 0;
                }
                Cap cap = _venture.CapOf(f.Place);
                long invested = 0;
                for (int h = 0; cap != null && h < cap.Holders.Count; h++)
                {
                    invested += cap.Holders[h].Kind == (int)HolderKind.Vc ? cap.Holders[h].Invested : 0;
                }
                _world.You.Add(new RivalryYou
                {
                    Place = f.Place, Name = firm.File.Name, Niche = f.Niche, Served = served, Market = market, Features = p.Features, Appeal = f.AppealNow, Fit = f.Fit,
                    Invested = invested, Stage = cap != null ? cap.Stage : -1
                });
            }
            _worldDay = day;
        }

        private void EnsureWorld()
        {
            if (RivalryOn && Save != null && _worldDay != Save.Day)
            {
                FillWorld(Save.Day);
            }
        }

        // ---------------------------------------------------------------- a day of reactions

        private void RivalryDay(int day)
        {
            if (!RivalryOn)
            {
                return;
            }
            FillWorld(day);
            RivalryRules.Day(_world, day, _ventureNotes);
            OpenBakes(day);
            for (int i = 0; i < _world.Sales.Count; i++)
            {
                Heard(_world.Sales[i], day);   // a startup a listed company bought in a big niche: the waterfall pays everyone, the player's lines into the wallet
            }
            for (int i = 0; i < _world.Told.Count; i++)
            {
                RivalTold(_world.Told[i]);
            }
            for (int i = 0; i < _world.Decided.Count; i++)
            {
                BakeDecided(_world.Decided[i], day);
            }
            for (int i = _rival.Seen.Count - 1; i >= 0; i--)
            {
                if (FirmOf(_rival.Seen[i].Place) == null)
                {
                    _rival.Seen.RemoveAt(i);
                }
            }
            if (_world.Told.Count + _world.Decided.Count + _world.Opened.Count + _world.Sales.Count > 0)
            {
                ChangedTech();
            }
            if (Verbose)
            {
                Logger?.Info("Tech check (competition, day " + day + "): " + _world.Told.Count + " reaction(s), " + _world.Opened.Count + " bake off(s) opened, " + _world.Decided.Count
                             + " decided, " + _world.Sales.Count + " sale(s) of startups: PASS");
            }
        }

        // What a reaction says to the player: a notice when it is against one of their firms, the paper when a listed company moves.
        private void RivalTold(RivalryReaction x)
        {
            FirmPlace firm = x.Place.Length > 0 ? FirmOf(x.Place) : null;
            string niche = x.Niche >= 0 ? TechText.T(NicheCatalogue.NameKey(x.Niche)) : string.Empty;
            string firmName = firm != null ? firm.File.Name : string.Empty;
            string row = firm != null ? TechApp.CapRow + x.Place : null;
            switch (x.Kind)
            {
                case ReactKind.Chest:
                    TellTech(NotificationType.Warning, "reallife_rival_note_chest", Data("rival", x.Name, "firm", firmName, "amount", TechText.Cents(Level(x.Amount))), TabFunding, row);
                    break;
                case ReactKind.Copying:
                    TellTech(NotificationType.Info, "reallife_rival_note_copying", Data("rival", x.Name, "firm", firmName, "days", x.Amount.ToString(CultureInfo.InvariantCulture)), TabFunding, row);
                    break;
                case ReactKind.Copied:
                    TellTech(NotificationType.Warning, "reallife_rival_note_copied", Data("rival", x.Name, "firm", firmName), TabFunding, row);
                    break;
                case ReactKind.Cut:
                    if (firm != null)
                    {
                        TellTech(NotificationType.Info, "reallife_rival_note_cut", Data("rival", x.Name, "firm", firmName, "niche", niche), TabFunding, row);
                    }
                    Story("reallife_rival_news_cut", "name", x.Name, "niche", niche);
                    break;
                case ReactKind.Backed:
                case ReactKind.Bought:
                    Story("reallife_rival_news_bought", "name", x.Name, "company", x.Other);
                    if (firm != null)
                    {
                        TellTech(NotificationType.Warning, "reallife_rival_note_bought", Data("rival", x.Name, "firm", firmName, "company", x.Other), TabFunding, row);
                    }
                    break;
                case ReactKind.Entered:
                    Story("reallife_rival_news_entered", "name", x.Other, "niche", niche);
                    if (firm != null)
                    {
                        TellTech(NotificationType.Warning, "reallife_rival_note_entered", Data("company", x.Other, "firm", firmName, "niche", niche), TabFunding, row);
                    }
                    break;
            }
        }

        // ---------------------------------------------------------------- what the pages show

        /// <summary>The rivals that sell into a niche today, startups and the listed companies that came in, into <paramref name="into"/> (cleared). Read when the page is drawn.</summary>
        internal int RivalsIn(int niche, List<RivalryLine> into)
        {
            into.Clear();
            if (_rival == null || _world == null || _sector == null || !_ventureOn)
            {
                return 0;
            }
            BindWorld();
            for (int i = 0; i < _sector.Rivals.Count; i++)
            {
                RivalProduct r = _sector.Rivals[i];
                RivalryRec rec = _rival.RecOf(r.Id);
                if (RivalryRules.NicheOf(_world, r, rec) != niche || !NicheRules.Serves(NicheCatalogue.Def(niche), r.Market))
                {
                    continue;
                }
                Startup s = r.Startups && r.Startup > 0 ? _venture.StartupOf(r.Startup) : null;
                long buyers = _world.NicheBuyers(niche, r.Market);
                int seg = Math.Max(0, Math.Min(2, SectorRules.BuyersOf(r.Market)));
                into.Add(new RivalryLine
                {
                    Id = r.Id, Name = r.Name, Title = r.Title, Company = rec != null && rec.Backer >= 0 ? HiringCatalogue.RivalFirms[rec.Backer] : r.Startups ? string.Empty : r.Name,
                    Listed = !r.Startups, Tier = r.Tier, Price = Level(FirmsCatalogue.Price[seg] * FirmsCatalogue.PriceTiers[Math.Max(0, Math.Min(2, r.Tier))] / 1000),
                    Share = buyers > 0 ? (int)Math.Min(1000L, Math.Max(0L, r.Customers) * 1000 / buyers) : 0, Customers = r.Customers, Cash = s != null && s.Alive ? Level(s.Cash) : -1,
                    Funded = RivalryRules.Funded(_world, r, rec)
                });
            }
            into.Sort((a, b) => b.Share.CompareTo(a.Share));
            return into.Count;
        }

        /// <summary>The reactions against one of the player's firms, newest first, into <paramref name="into"/> (cleared).</summary>
        internal int ReactionsOf(string place, List<RivalryReaction> into)
        {
            into.Clear();
            for (int i = 0; _rival != null && _ventureOn && i < _rival.Log.Count; i++)
            {
                if (_rival.Log[i].Place == place)
                {
                    into.Add(_rival.Log[i]);
                }
            }
            return into.Count;
        }

        /// <summary>Who is coming at a firm today, by what they are doing: the funded rivals in its niches, by the pull they bring to hiring.</summary>
        internal string RivalAgainst(string place, out int pull)
        {
            pull = 1000;
            if (!RivalryOn)
            {
                return null;
            }
            BindWorld();
            for (int i = 0; i < _world.You.Count; i++)
            {
                if (_world.You[i].Place == place)
                {
                    RivalProduct r = RivalryRules.Poacher(_world, _world.You[i].Served, out pull);
                    return r != null ? r.Name : null;
                }
            }
            return null;
        }

        // ---------------------------------------------------------------- the poaching pull

        /// <summary>A funded rival in the niche of the firm's product pulls on its people harder (thousandths for TalentRules.Approached); 1000 where none does.</summary>
        private int RivalryPull(string place)
        {
            return RivalAgainst(place, out int pull) != null ? pull : 1000;
        }
    }
}
