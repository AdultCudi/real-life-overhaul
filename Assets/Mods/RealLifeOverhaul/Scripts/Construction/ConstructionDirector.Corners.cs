using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Construction
{
    /// <summary>
    /// The corner buildings on the construction game's side (plans/build-mode.md section 12; the owner, 26 Oct 2026, seven corner
    /// buildings chosen): a site at a block's corner over two streets (CellRules.Corner) is offered each corner building that fits it
    /// for its use (ConstructionBridges.CornerOffers, Scripts/Corners), each worn over a design of the city's own of its use's type
    /// that fits the ground the corner building leaves it, besides the city's designs; the project wears it (ProjectFile.Look) and
    /// hands it to the district with its building (FinishLot's look); the city's developers and its redevelopments pick among the
    /// same offers on corner lots, by the same rules and costs. Never in a Going Public session for the city's own choices, so every
    /// machine's developers pick as before there.
    /// COST: nothing per frame; asked when a footprint is picked, a design is drawn, or a development decides its design.
    /// </summary>
    public sealed partial class ConstructionDirector
    {
        private readonly List<CornerOffer> _cornerOffers = new List<CornerOffer>(8);
        private readonly List<DesignChoice> _cornerCopies = new List<DesignChoice>(DesignsAsked);

        /// <summary>
        /// The corner buildings that stand on a corner site's ground for a use, appended to <paramref name="into"/>: each worn over the
        /// design of the use's type that best fills the ground left beneath it; a model with no such design is not offered. False while
        /// the city's designs are being looked through.
        /// </summary>
        public bool CornerDesigns(LotView site, bool low, float edge, BuildUse use, List<DesignChoice> into)
        {
            _cornerOffers.Clear();
            if (LandRules.SiteFit(site.Width, site.Depth, out _) != PlotFit.Fits)
            {
                return true;   // known: none, on land no site can be set out on
            }
            if (!AskCorners(CellRules.CornerGround(site, low, edge), _cornerOffers))
            {
                return true;   // known: none
            }
            bool ready = true;
            for (int i = 0; i < _cornerOffers.Count; i++)
            {
                CornerOffer o = _cornerOffers[i];
                if ((o.Uses & (1 << (int)use)) == 0)
                {
                    continue;   // not offered for this use
                }
                if (!ReadDesigns(o.CopyWidth, o.CopyDepth, BuildCatalogue.UseTypes[(int)use], DesignsAsked, _cornerCopies))
                {
                    ready = false;
                    continue;
                }
                if (_cornerCopies.Count == 0)
                {
                    continue;   // no design of the type fits the ground it leaves: its building would have no interior
                }
                DesignChoice d = _cornerCopies[0];   // the best covering
                d.Look = o.Look;
                d.LookKey = o.NameKey;
                d.LookX = o.X;
                d.LookZ = o.Z;
                d.LookYaw = o.Yaw;
                d.Height = o.Height;   // the storeys the plans and the building code count are the corner building's
                into.Add(d);
            }
            return ready;
        }

        // The corner buildings offered on a corner's ground; false for none (the corner buildings not there, or a fault, said once).
        private static bool AskCorners(CellCorner ground, List<CornerOffer> into)
        {
            into.Clear();
            try
            {
                if (Down("CornerOffers") || ConstructionBridges.CornerOffers == null)
                {
                    return false;
                }
                ConstructionBridges.CornerOffers(ground, into);
                return into.Count > 0;
            }
            catch (Exception e)
            {
                into.Clear();
                Faulty("CornerOffers", e);
                return false;
            }
        }

        /// <summary>A corner building's name in the player's language (its model's id), or null for none.</summary>
        public static string CornerName(string look)
        {
            if (string.IsNullOrEmpty(look))
            {
                return null;
            }
            try
            {
                return !Down("CornerName") && ConstructionBridges.CornerName != null ? ConstructionBridges.CornerName(look) : null;
            }
            catch (Exception e)
            {
                Faulty("CornerName", e);
                return null;
            }
        }

        // The corner buildings a development or the city's redevelopment may choose on a lot at a block's corner, after the city's designs
        // that fill it: the same offers as the player's, never in a Going Public session (every machine then picks as before).
        private bool CityCorners(LotView lot, BuildUse use, List<DesignChoice> into)
        {
            if (GoingPublic.InSession || !CellRules.Corner(lot, out bool low, out float edge))
            {
                return true;
            }
            _cityCorners.Clear();
            bool ready = CornerDesigns(lot, low, edge, use, _cityCorners);
            into.AddRange(_cityCorners);
            return ready;
        }

        private readonly List<DesignChoice> _cityCorners = new List<DesignChoice>(8);

        // The name a development or a redevelopment keeps for its design: the corner building's model, or the city's design.
        private static string KeptName(DesignChoice d) => string.IsNullOrEmpty(d.Look) ? d.Name : d.Look;

        // A design kept by name (a corner building's model, or a design of the city's) among those offered.
        private static bool Named(DesignChoice d, string name) => string.IsNullOrEmpty(d.Look) ? d.Name == name : d.Look == name;
    }
}
