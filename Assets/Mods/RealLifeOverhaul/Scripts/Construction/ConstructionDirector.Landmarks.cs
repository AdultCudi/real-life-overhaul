using System;
using System.Collections.Generic;
using UI.Notification;

namespace RealLifeOverhaul.Construction
{
    /// <summary>
    /// The director's part for Uptown's landmarks (Scripts/Landmarks, through ConstructionBridges): a landmark's tender, put
    /// out by the landmarks' own timetable and awarded with the others; its site as one of the construction game's lots, so
    /// the player's win is a project on its ground; its lots held, so nobody buys or develops them; and the programme's
    /// questions about the works a rival or the city's own works run for it, answered by the landmarks. Each on a day's
    /// pass, a page, a press or a site's working hour, never per frame.
    /// </summary>
    public sealed partial class ConstructionDirector
    {
        private readonly List<LotView> _landmarkSites = new List<LotView>(4);

        /// <summary>Each landmark's site with its ground settled, as one of the construction game's lots: read afresh on each ask, three at most.</summary>
        public IReadOnlyList<LotView> LandmarkSites
        {
            get
            {
                _landmarkSites.Clear();
                try
                {
                    if (!Down("LandmarkSites") && ConstructionBridges.LandmarkSites != null)
                    {
                        ConstructionBridges.LandmarkSites(_landmarkSites);
                    }
                }
                catch (Exception e)
                {
                    _landmarkSites.Clear();
                    Faulty("LandmarkSites", e);
                }
                return _landmarkSites;
            }
        }

        // A landmark's site by its id; false for any other lot.
        private bool LandmarkSite(string id, out LotView lot)
        {
            IReadOnlyList<LotView> sites = id != null ? LandmarkSites : null;
            for (int i = 0; sites != null && i < sites.Count; i++)
            {
                if (sites[i].Id == id)
                {
                    lot = sites[i];
                    return true;
                }
            }
            lot = default(LotView);
            return false;
        }

        /// <summary>True for the id of a landmark's site, not a lot of the district's.</summary>
        public bool IsLandmark(string id) => LandmarkSite(id, out _);

        /// <summary>
        /// A landmark put out to tender (the landmarks' timetable asks): its site, its use, floor area and storeys, priced as a
        /// public building with the landmark's premium, open four days. The tender's id, or 0 with why: the construction game
        /// not running, the developers' and tenders' option off, a Going Public session (the city builds it on its own timetable
        /// on every machine), the site not known.
        /// </summary>
        public int LandmarkTender(string site, int use, int area, int storeys, out string why)
        {
            why = null;
            if (!Running)
            {
                why = "the construction game is not running";
                return 0;
            }
            if (!Settings.ConstructionDevelopers)
            {
                why = "the developers' and tenders' option is off";
                return 0;
            }
            if (GoingPublic.InSession)
            {
                why = "in a Going Public session no landmark goes out to tender";
                return 0;
            }
            if (!LandmarkSite(site, out LotView lot))
            {
                why = "the landmark's site is not known";
                return 0;
            }
            for (int i = 0; i < _state.Tenders.Count; i++)
            {
                if (_state.Tenders[i].Source == TenderSource.Landmark && _state.Tenders[i].Lot == site && _state.Tenders[i].State == TenderState.Open)
                {
                    return _state.Tenders[i].Id;   // already out: asked again after a load
                }
            }
            BuildUse kind = (BuildUse)BuildCatalogue.Clamp(use, 0, (int)BuildUse.Count - 1);
            int floors = Math.Max(1, storeys);
            int now = ConstructionGame.Day * 24 + ConstructionGame.Hour;
            var t = new TenderFile
            {
                Id = _state.NewId(), Source = TenderSource.Landmark, CivicKind = 5, Lot = site, Street = lot.Street ?? string.Empty, Number = lot.Number, Use = kind,
                Area = Math.Max(1, area), Hours = PlansRules.Hours(kind, Math.Max(1, area), floors, ProjectKind.Plot), Opened = now, Closes = now + BuildCatalogue.LandmarkTenderHours
            };
            Exchange(_exchange);
            long cost = PlansRules.Estimate(ProjectKind.Plot, kind, t.Area, floors, true, PlansRules.ExitsNeeded(floors, t.Area), ConstructionGame.Prices, _exchange).Total;
            t.Value = TenderRules.BuildingValue(cost) * (1000L + BuildCatalogue.LandmarkPremiumPermille) / 1000L / 1000L * 1000L;
            AddTender(t);
            Changed();
            if (_state.Firm.Founded > 0)
            {
                Tell(NotificationType.Info, "reallife_lmk_notice_tender", TenderName(t), null, TabTenders, t.Id);
            }
            return t.Id;
        }

        /// <summary>The state of a landmark's tender by its id: open, or how it ended; Gone for one not kept.</summary>
        public TenderState LandmarkTenderState(int id, out int winner)
        {
            winner = -2;
            for (int i = 0; i < _state.Tenders.Count; i++)
            {
                if (_state.Tenders[i].Id == id)
                {
                    winner = _state.Tenders[i].Winner;
                    return _state.Tenders[i].State;
                }
            }
            return TenderState.Gone;
        }

        /// <summary>A landmark's open tender taken back (the landmarks' tests put back): false for one not open.</summary>
        public bool DropLandmarkTender(int id)
        {
            for (int i = 0; i < _state.Tenders.Count; i++)
            {
                TenderFile t = _state.Tenders[i];
                if (t.Id == id && t.Source == TenderSource.Landmark && t.State == TenderState.Open)
                {
                    _state.Tenders.RemoveAt(i);
                    Changed();
                    return true;
                }
            }
            return false;
        }

        /// <summary>A landmark's open tender closed now and awarded (a test brings the day forward): false for one not open.</summary>
        public bool AwardLandmarkTender(int id)
        {
            for (int i = 0; i < _state.Tenders.Count; i++)
            {
                TenderFile t = _state.Tenders[i];
                if (t.Id == id && t.Source == TenderSource.Landmark && t.State == TenderState.Open)
                {
                    Award(t, ConstructionGame.Day);
                    Changed();
                    return true;
                }
            }
            return false;
        }

        /// <summary>True for a lot somebody has a claim on in the construction game: the player's land, a project on it, a development of the city's developers.</summary>
        public bool Claimed(string lot)
        {
            return _state != null && lot != null && (_state.Lot(lot) != null || _state.OnLot(lot) != null || _state.Development(lot) != null);
        }

        // ------------------------------------------------------------------ the asks

        internal static bool LandmarkHeld(string lot)
        {
            try
            {
                return !Down("LandmarkHeld") && ConstructionBridges.LandmarkHeld != null && lot != null && ConstructionBridges.LandmarkHeld(lot);
            }
            catch (Exception e)
            {
                Faulty("LandmarkHeld", e);
                return false;
            }
        }

        internal static string LandmarkName(string site)
        {
            try
            {
                return !Down("LandmarkName") && ConstructionBridges.LandmarkName != null ? ConstructionBridges.LandmarkName(site) : null;
            }
            catch (Exception e)
            {
                Faulty("LandmarkName", e);
                return null;
            }
        }

        internal static string LandmarkWords(string site)
        {
            try
            {
                return !Down("LandmarkWords") && ConstructionBridges.LandmarkWords != null ? ConstructionBridges.LandmarkWords(site) : null;
            }
            catch (Exception e)
            {
                Faulty("LandmarkWords", e);
                return null;
            }
        }

        internal static bool LandmarkDesign(string site, out DesignChoice design)
        {
            design = default(DesignChoice);
            try
            {
                return !Down("LandmarkDesign") && ConstructionBridges.LandmarkDesign != null && ConstructionBridges.LandmarkDesign(site, out design);
            }
            catch (Exception e)
            {
                Faulty("LandmarkDesign", e);
                return false;
            }
        }

        internal static void LandmarkAwarded(string site, int winner, long price, int days)
        {
            try
            {
                if (!Down("LandmarkAwarded"))
                {
                    ConstructionBridges.LandmarkAwarded?.Invoke(site, winner, price, days);
                }
            }
            catch (Exception e)
            {
                Faulty("LandmarkAwarded", e);
            }
        }

        internal static bool LandmarkHanded(string site)
        {
            try
            {
                return !Down("LandmarkHanded") && ConstructionBridges.LandmarkHanded != null && ConstructionBridges.LandmarkHanded(site);
            }
            catch (Exception e)
            {
                Faulty("LandmarkHanded", e);
                return false;
            }
        }

        internal static string LandmarkWorks(int siteId)
        {
            try
            {
                return !Down("LandmarkWorks") && ConstructionBridges.LandmarkWorks != null && siteId > 0 ? ConstructionBridges.LandmarkWorks(siteId) : null;
            }
            catch (Exception e)
            {
                Faulty("LandmarkWorks", e);
                return null;
            }
        }

        internal static float LandmarkPace(int siteId, double done, double needed, float hours)
        {
            try
            {
                return !Down("LandmarkPace") && ConstructionBridges.LandmarkPace != null ? ConstructionBridges.LandmarkPace(siteId, done, needed, hours) : hours;
            }
            catch (Exception e)
            {
                Faulty("LandmarkPace", e);
                return hours;
            }
        }

        internal static bool LandmarkBuilt(string site)
        {
            try
            {
                return !Down("LandmarkBuilt") && ConstructionBridges.LandmarkBuilt != null && ConstructionBridges.LandmarkBuilt(site);
            }
            catch (Exception e)
            {
                Faulty("LandmarkBuilt", e);
                return false;
            }
        }
    }
}
