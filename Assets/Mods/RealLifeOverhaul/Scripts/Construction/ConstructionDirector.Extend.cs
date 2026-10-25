using System;
using System.Collections.Generic;
using UI.Notification;

namespace RealLifeOverhaul.Construction
{
    /// <summary>
    /// The director's part for extensions and changes of use (docs/features/construction.md, "Extensions and change of use").
    /// An extension: more storeys on an Uptown building of the player's, up to the building code's limit in force; plans by an
    /// architect, planning permission (the neighbours object to the height it adds), an engineer's sign off, the rebuild's
    /// scaffolding to the roof with the business inside open throughout; at the handover its floor area grows (the district's
    /// record and the deed, now and from every load), its front its own. A change of use: an Uptown building of the player's made
    /// another use its structure takes; planning permission for the change (the council's odds for the use on its lot), notice
    /// to its tenants and business with the application as for a demolition, the programme's refit works stripping it to its
    /// shell and fitting it out; at the handover an empty unit of the player's of the new use, to furnish or to let. Never in a
    /// Going Public session. On a press and the day's pass; nothing per frame.
    /// </summary>
    public sealed partial class ConstructionDirector
    {
        private readonly List<string> _takes = new List<string>(4);
        private readonly List<BuildUse> _convertTo = new List<BuildUse>(4);

        /// <summary>The storeys Uptown allows under the building code in force (the elected government's).</summary>
        public int UptownStoreys()
        {
            return PlansRules.HeightLimit(string.Empty, true, Permits());
        }

        // ------------------------------------------------------------------ an extension

        /// <summary>Why the player's building at an address cannot be given more storeys now, as a text key; null when it can.</summary>
        public string ExtendBlock(string street, int number)
        {
            string why = RedevelopRefusal(street, number, out StandingView b);
            if (why != null)
            {
                return why;
            }
            return RedevelopRules.ExtensionRoom(b.Storeys, UptownStoreys()) > 0 ? null : "reallife_bld_answer_extension_no_room";
        }

        /// <summary>An extension of the player's building in Uptown by a storey, its plans still to draw. Its id, or 0 with why not.</summary>
        public int NewExtension(string street, int number, out string why)
        {
            why = ExtendBlock(street, number);
            if (why != null || !Room(out why))
            {
                return 0;
            }
            Standing(street, number, out StandingView b);
            ConstructionGame.Building(street, number, out BuildingView view);
            int use = BuildCatalogue.UseOf(b.Type);
            var p = new ProjectFile
            {
                Id = _state.NewId(), Kind = ProjectKind.Extension, Stage = ProjectStage.Design, Created = ConstructionGame.Day, Street = street, Number = number, Lot = b.Lot,
                District = view.District ?? string.Empty, Use = use >= 0 ? (BuildUse)use : BuildUse.Offices, Size = b.Size, Version = b.Version, Height = b.Height,
                Design = b.Design ?? string.Empty, Architect = ArchitectKind.Firm, Firm = 1
            };
            Raise(p, b, 1);
            _state.Projects.Add(p);
            Logger?.Info("Construction: project " + p.Id + " made to extend " + street + " " + number + " (lot " + b.Lot + ", " + b.Storeys + " storey(s), " + b.Sqm + " m2) by a storey: "
                         + p.Area + " m2 more.");
            Changed();
            return p.Id;
        }

        // An extension's storeys on top, its storeys in all and the floor area it adds, from its building as it stands.
        private static void Raise(ProjectFile p, StandingView b, int added)
        {
            p.Added = Math.Max(1, added);
            p.Storeys = Math.Max(1, b.Storeys) + p.Added;
            p.Area = RedevelopRules.AddedArea(b.SizeSqm, p.Storeys, b.Sqm);
        }

        /// <summary>Storeys an extension may put on its building now: up to the building code's limit in force; 0 for none.</summary>
        public int ExtensionRoomOf(ProjectFile p)
        {
            return p != null && Standing(p.Street, p.Number, out StandingView b) ? RedevelopRules.ExtensionRoom(b.Storeys, UptownStoreys()) : 0;
        }

        /// <summary>The storeys an extension puts on top, chosen before its plans are asked for. Returns a text key, or null when set.</summary>
        public string SetAdded(ProjectFile p, int added)
        {
            if (p == null || p.Kind != ProjectKind.Extension || p.Stage != ProjectStage.Design || p.PlansDay > 0 || p.PlansDone)
            {
                return "reallife_bld_answer_not_design";
            }
            if (!Standing(p.Street, p.Number, out StandingView b))
            {
                return "reallife_bld_answer_not_district";
            }
            if (added < 1 || added > RedevelopRules.ExtensionRoom(b.Storeys, UptownStoreys()))
            {
                return "reallife_bld_answer_extension_no_room";
            }
            Raise(p, b, added);
            Changed();
            return null;
        }

        // ------------------------------------------------------------------ a change of use

        /// <summary>Why the player's building at an address cannot be made another use now, as a text key; null when it can.</summary>
        public string ConvertBlock(string street, int number)
        {
            string why = RedevelopRefusal(street, number, out StandingView b);
            if (why != null)
            {
                return why;
            }
            ConversionUses(b.Size, b.Version, b.Type, _convertTo);
            return _convertTo.Count == 0 ? "reallife_bld_answer_conversion_none" : ConstructionGame.Occupied(street, number);   // empty of their own things first
        }

        /// <summary>The uses a building of a structure and type may be made: the game's types its size and version take, other than its own.</summary>
        public void ConversionUses(string size, int version, string type, List<BuildUse> into)
        {
            into.Clear();
            ConstructionGame.Takes(size, version, _takes);
            for (int u = 0; u < (int)BuildUse.Count; u++)
            {
                string use = BuildCatalogue.UseTypes[u];
                if (_takes.Contains(use) && !string.Equals(use, type, StringComparison.Ordinal))
                {
                    into.Add((BuildUse)u);
                }
            }
        }

        /// <summary>A change of use of the player's building in Uptown, its permission and notice still to ask for. Its id, or 0 with why not.</summary>
        public int NewConversion(string street, int number, out string why)
        {
            why = ConvertBlock(street, number);
            if (why != null || !Room(out why))
            {
                return 0;
            }
            Standing(street, number, out StandingView b);
            ConstructionGame.Building(street, number, out BuildingView view);
            ConversionUses(b.Size, b.Version, b.Type, _convertTo);
            var p = new ProjectFile
            {
                Id = _state.NewId(), Kind = ProjectKind.Conversion, Stage = ProjectStage.Design, Created = ConstructionGame.Day, Street = street, Number = number, Lot = b.Lot,
                District = view.District ?? string.Empty, Use = _convertTo[0], Size = b.Size, Version = b.Version, Area = Math.Max(1, b.Sqm), Storeys = Math.Max(1, b.Storeys),
                Height = b.Height, Design = b.Design ?? string.Empty, Hours = RedevelopRules.ConversionHours(b.Sqm), CivicKind = RedevelopRules.ConversionWorks,
                Source = TenderSource.Client, Quality = 70, PlansDone = true, Engineered = true
            };
            p.Estimate = ConversionEstimate(p);
            _state.Projects.Add(p);
            Logger?.Info("Construction: project " + p.Id + " made to change " + street + " " + number + " (lot " + b.Lot + ") from " + b.Type + " to " + BuildCatalogue.UseTypes[(int)p.Use]
                         + ": " + p.Hours + " working hours, estimated at " + p.Estimate + ".");
            Changed();
            return p.Id;
        }

        // What a change of use costs its builder: the programme's refit works for its hours.
        private long ConversionEstimate(ProjectFile p)
        {
            Exchange(_exchange);
            return PlansRules.EstimateWorks(RedevelopRules.ConversionWorks, p.Hours, ConstructionGame.Prices, _exchange).Total;
        }

        /// <summary>The use a change of use makes its building, chosen before its permission is asked for. Returns a text key, or null when set.</summary>
        public string SetConversionUse(ProjectFile p, BuildUse use)
        {
            if (p == null || p.Kind != ProjectKind.Conversion || p.Stage != ProjectStage.Design)
            {
                return "reallife_bld_answer_not_design";
            }
            if (!Standing(p.Street, p.Number, out StandingView b))
            {
                return "reallife_bld_answer_not_district";
            }
            ConversionUses(b.Size, b.Version, b.Type, _convertTo);
            if (!_convertTo.Contains(use))
            {
                return "reallife_bld_answer_conversion_none";
            }
            p.Use = use;
            Changed();
            return null;
        }

        /// <summary>The uses a change of use may make its building, in the order its selector lists them.</summary>
        public void ConversionUsesOf(ProjectFile p, List<BuildUse> into)
        {
            into.Clear();
            if (p != null && Standing(p.Street, p.Number, out StandingView b))
            {
                ConversionUses(b.Size, b.Version, b.Type, into);
            }
        }

        /// <summary>True when the council would hear a change to a use the lot's class is zoned for (its odds are the better for it).</summary>
        private bool ConversionZoned(ProjectFile p)
        {
            return Lot(p.Lot, out LotView lot) && LandRules.Zoned(LandRules.ZonesOf(lot.Class), p.Use);
        }

        // ------------------------------------------------------------------ the handover

        /// <summary>True for a site whose building keeps its own front at the handover: an extension's (more floors, the same look).</summary>
        public bool KeepsLook(int siteId)
        {
            ProjectFile p = _state != null ? _state.OnSite(siteId) : null;
            RedevFile r = _state != null && p == null ? _state.CityRedevSite(siteId) : null;
            return (p != null && p.Kind == ProjectKind.Extension) || (r != null && r.Move == CityMove.Extend);   // the city's extensions too
        }

        /// <summary>
        /// An extension or a change of use handed over to the district: its building's floor area or its use changed, now and from
        /// every load. False while the district cannot take it yet (the player inside a building changing use, the district not
        /// standing at this load): asked again each day, with a notice when it is done later.
        /// </summary>
        private bool Redeveloped(ProjectFile p, bool atHandover = false)
        {
            if (p.InDistrict)
            {
                return true;
            }
            bool extension = p.Kind == ProjectKind.Extension;
            string why;
            try
            {
                why = extension
                    ? Down("Extend") || ConstructionBridges.Extend == null ? "the district is not there to take it" : ConstructionBridges.Extend(p.Lot, p.Storeys)
                    : Down("Convert") || ConstructionBridges.Convert == null ? "the district is not there to take it" : ConstructionBridges.Convert(p.Lot, BuildCatalogue.UseTypes[(int)p.Use]);
            }
            catch (Exception e)
            {
                Faulty(extension ? "Extend" : "Convert", e);
                why = "a fault";
            }
            if (why != null)
            {
                Logger?.Info("Construction: project " + p.Id + "'s building is not " + (extension ? "extended" : "changed in use") + " in the district yet (" + why + "); asked again each day.");
                return false;
            }
            p.InDistrict = true;
            if (!atHandover)
            {
                Tell(NotificationType.Success, extension ? "reallife_bld_notice_extended" : "reallife_bld_notice_converted", ProjectName(p), null, TabProjects, p.Id);
            }
            Logger?.Info("Construction: project " + p.Id + "'s building " + (extension ? "has " + p.Storeys + " storeys of floor area now" : "is of " + BuildCatalogue.UseTypes[(int)p.Use] + " now")
                         + ", and from every load.");
            return true;
        }
    }
}
