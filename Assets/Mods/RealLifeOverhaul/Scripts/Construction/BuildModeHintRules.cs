using System;

namespace RealLifeOverhaul.Construction
{
    /// <summary>What the player holds in the district, as the toolbar reads it (from the columns' facts). Saved nowhere.</summary>
    public struct BuildHoldings
    {
        public int Land;        // columns of bare land of the player's (bought, no building of theirs standing on it)
        public int Placeable;   // ... of them free for a building: no project on them, and a use in force there
        public int Unzoned;     // ... free of a project but with no use in force (zoned for nothing, or only a use asked of City Hall)
        public int Buildings;   // columns under a building of the player's
    }

    /// <summary>Where build mode stands, as the hint bar reads it. Saved nowhere.</summary>
    public struct HintFacts
    {
        public BuildModeState State;
        public bool Picked;           // squares are picked
        public bool SiteOk;           // ... and they make a site by the site rule
        public bool CanBuy;           // ... which may be bought now (the Land tab's own checks)
        public bool PickedMine;       // ... which is the player's own land (Zone, Join, Place)
        public bool PickedForSale;    // ... which is land for sale (Zone, Join, Place: buy it first)
        public bool PickedBuilding;   // ... under a building of the player's (Bulldoze, Refurbish)
        public bool ZoneChosen;       // the Zone tool: a zone's swatch is chosen
        public int Designs;           // the Place tool: designs that fit the squares picked
        public bool MayBuild;         // ... the design picked has nothing in red that stops it
    }

    /// <summary>The tour's steps on build mode's first opening: each a tooltip pointing at one part.</summary>
    public enum TourStep
    {
        HintBar = 0,   // the line at the top that says what to do next
        Toolbar = 1,   // the tools at the bottom, in the order they are used
        Views = 2,     // the view toggles beside them
        Demand = 3,    // the demand bars
        Done = 4
    }

    /// <summary>
    /// Build mode's guidance (the owner, 26 Oct 2026: "when a new user sees it they must not feel where to click"): which tools
    /// can be used yet and why not, the one line at the top that says what to do next in every state with what Esc does there, the
    /// tour's order and the demand bars' words. Game-free (EconomyCheck's BuildModeChecks), the same on every machine.
    /// COST: nothing per frame; a switch or a loop over the columns' facts when something changes.
    /// </summary>
    public static class BuildModeHintRules
    {
        public const string WhySession = "reallife_bld_hint_why_session";
        public const string WhyBuyFirst = "reallife_bld_hint_why_buy_first";
        public const string WhyBuyZoneFirst = "reallife_bld_hint_why_buy_zone_first";
        public const string WhyZoneFirst = "reallife_bld_hint_why_zone_first";
        public const string WhyAllUsed = "reallife_bld_hint_why_all_used";
        public const string WhyNoBuilding = "reallife_bld_hint_why_no_building";
        public const string WhyNoSites = "reallife_bld_hint_why_no_sites";
        public const string BuyFirst = "reallife_bld_hint_buy_first";   // Zone, Place or Join on land for sale: buy it first

        /// <summary>Each tool's name key, by BuildTool, in the toolbar's order (the order the steps are taken).</summary>
        public static readonly string[] ToolKeys =
        {
            "reallife_bld_mode_tool_land", "reallife_bld_mode_tool_zone", "reallife_bld_mode_tool_place", "reallife_bld_mode_tool_bulldoze", "reallife_bld_mode_tool_join",
                "reallife_bld_mode_tool_refurbish"
        };

        /// <summary>Each tool's one line for its tooltip, by BuildTool.</summary>
        public static readonly string[] ToolTips =
        {
            "reallife_bld_tip_tool_land", "reallife_bld_tip_tool_zone", "reallife_bld_tip_tool_place", "reallife_bld_tip_tool_bulldoze", "reallife_bld_tip_tool_join",
                "reallife_bld_tip_tool_refurbish"
        };

        /// <summary>Each view's one line for its tooltip, by BuildView.</summary>
        public static readonly string[] ViewTips =
        {
            "reallife_bld_tip_view_status", "reallife_bld_tip_view_zoning", "reallife_bld_tip_view_value", "reallife_bld_tip_view_occupancy", "reallife_bld_tip_view_underway"
        };

        /// <summary>What the player holds, from every column's facts (the first <paramref name="count"/>).</summary>
        public static BuildHoldings Holdings(BuildModeRules.ColumnFacts[] facts, int count)
        {
            var h = new BuildHoldings();
            for (int c = 0; facts != null && c < count && c < facts.Length; c++)
            {
                BuildModeRules.ColumnFacts f = facts[c];
                if (f.MyBuilding)
                {
                    h.Buildings++;
                }
                if (!f.Mine)
                {
                    continue;
                }
                h.Land++;
                if (f.UnderWay || f.HasProject)
                {
                    continue;
                }
                if (InForce(f))
                {
                    h.Placeable++;
                }
                else
                {
                    h.Unzoned++;
                }
            }
            return h;
        }

        /// <summary>True when a column of the player's has a use in force: a use painted and allowed, or none painted and the council's zoning gives one.</summary>
        public static bool InForce(BuildModeRules.ColumnFacts f) => f.Paint > 0 ? !f.PaintWaits : f.Zones != 0;

        /// <summary>
        /// Why a tool cannot be used yet (its tooltip's line and the reason it is greyed), or null when it can. Buy land always can
        /// (in a Going Public session only to look at prices); every other tool needs something of the player's first, in the order
        /// the steps are taken.
        /// </summary>
        public static string ToolWhy(BuildTool tool, BuildHoldings h, bool viewOnly)
        {
            if (tool == BuildTool.Land)
            {
                return null;
            }
            if (viewOnly)
            {
                return WhySession;
            }
            switch (tool)
            {
                case BuildTool.Zone:
                    return h.Land > 0 ? null : WhyBuyFirst;
                case BuildTool.Join:
                    return h.Land > 0 ? null : WhyNoSites;
                case BuildTool.Place:
                    if (h.Land == 0)
                    {
                        return WhyBuyZoneFirst;
                    }
                    if (h.Placeable > 0)
                    {
                        return null;
                    }
                    return h.Unzoned > 0 ? WhyZoneFirst : WhyAllUsed;
                case BuildTool.Bulldoze:
                case BuildTool.Refurbish:
                    return h.Buildings > 0 ? null : WhyNoBuilding;
                default:
                    return WhyBuyFirst;
            }
        }

        /// <summary>True when the tool can be used now.</summary>
        public static bool ToolOpen(BuildTool tool, BuildHoldings h, bool viewOnly) => ToolWhy(tool, h, viewOnly) == null;

        /// <summary>The tool to open on: the first in the steps' order the player has not done yet (no land: Buy land; land but no building: Zone or Place).</summary>
        public static BuildTool FirstTool(BuildHoldings h, bool viewOnly)
        {
            if (viewOnly || h.Land == 0)
            {
                return BuildTool.Land;
            }
            if (h.Placeable > 0)
            {
                return BuildTool.Place;
            }
            return h.Unzoned > 0 ? BuildTool.Zone : BuildTool.Land;
        }

        /// <summary>The hint bar's line for where build mode stands: what to do next, in plain words. Its {cells} is the squares picked.</summary>
        public static string Hint(HintFacts f)
        {
            BuildModeState s = f.State;
            if (!s.Open)
            {
                return null;
            }
            if (s.ViewOnly)
            {
                return "reallife_bld_hint_view_only";
            }
            if (s.Step == BuildStep.Confirming)
            {
                return "reallife_bld_hint_confirming";
            }
            bool dragging = s.Step == BuildStep.Dragging;
            switch (s.Tool)
            {
                case BuildTool.Land:
                    if (dragging)
                    {
                        return "reallife_bld_hint_land_drag";
                    }
                    if (!f.Picked)
                    {
                        return "reallife_bld_hint_land_idle";
                    }
                    if (!f.SiteOk)
                    {
                        return "reallife_bld_hint_land_bad";
                    }
                    return f.CanBuy ? "reallife_bld_hint_land_buy" : "reallife_bld_hint_land_blocked";
                case BuildTool.Zone:
                    if (dragging || !f.Picked)
                    {
                        return f.ZoneChosen ? "reallife_bld_hint_zone_click" : "reallife_bld_hint_zone_pick";
                    }
                    if (!f.PickedMine)
                    {
                        return f.PickedForSale ? BuyFirst : "reallife_bld_hint_zone_not_yours";
                    }
                    return f.ZoneChosen ? "reallife_bld_hint_zone_click" : "reallife_bld_hint_zone_pick_for";
                case BuildTool.Place:
                    if (dragging || !f.Picked)
                    {
                        return "reallife_bld_hint_place_idle";
                    }
                    if (!f.PickedMine)
                    {
                        return f.PickedForSale ? BuyFirst : "reallife_bld_hint_place_not_yours";   // whose land comes first: the site rule's words would not help
                    }
                    if (!f.SiteOk)
                    {
                        return "reallife_bld_hint_place_bad";
                    }
                    if (s.Step == BuildStep.Ghost)
                    {
                        return f.MayBuild ? "reallife_bld_hint_place_build" : "reallife_bld_hint_place_fix";
                    }
                    return f.Designs > 0 ? "reallife_bld_hint_place_pick" : "reallife_bld_hint_place_none";
                case BuildTool.Join:
                    if (dragging || !f.Picked)
                    {
                        return "reallife_bld_hint_join_idle";
                    }
                    return f.PickedMine ? "reallife_bld_hint_join_go" : f.PickedForSale ? BuyFirst : "reallife_bld_hint_zone_not_yours";
                case BuildTool.Bulldoze:
                    if (dragging || !f.Picked)
                    {
                        return "reallife_bld_hint_bulldoze_idle";
                    }
                    return f.PickedBuilding ? "reallife_bld_hint_bulldoze_go" : "reallife_bld_hint_building_not_yours";
                case BuildTool.Refurbish:
                    if (dragging || !f.Picked)
                    {
                        return "reallife_bld_hint_refurbish_idle";
                    }
                    return f.PickedBuilding ? "reallife_bld_hint_refurbish_go" : "reallife_bld_hint_building_not_yours";
                default:
                    return "reallife_bld_hint_land_idle";
            }
        }

        /// <summary>
        /// The step number in the hint bar's circle: the steps from bare ground to a building (pick land 1, buy it 2, zone it 3, pick
        /// where the building goes 4, build it 5); 0 for a tool off that path (bulldoze, join, refurbish) and for view only.
        /// </summary>
        public static int HintStep(HintFacts f)
        {
            BuildModeState s = f.State;
            if (!s.Open || s.ViewOnly)
            {
                return 0;
            }
            switch (s.Tool)
            {
                case BuildTool.Land:
                    return f.Picked && s.Step != BuildStep.Dragging ? 2 : 1;
                case BuildTool.Zone:
                    return 3;
                case BuildTool.Place:
                    return s.Step == BuildStep.Ghost || s.Step == BuildStep.Confirming ? 5 : 4;
                default:
                    return 0;
            }
        }

        /// <summary>The hint bar's second line before what Esc does: a tip for where build mode stands, or null.</summary>
        public static string HintExtra(HintFacts f)
        {
            BuildModeState s = f.State;
            if (!s.Open || s.ViewOnly || s.Step == BuildStep.Confirming)
            {
                return null;
            }
            switch (s.Tool)
            {
                case BuildTool.Land:
                    return f.Picked || s.Step == BuildStep.Dragging ? "reallife_bld_hint_extra_drop" : "reallife_bld_hint_extra_drag";
                case BuildTool.Zone:
                    return "reallife_bld_hint_extra_own";
                case BuildTool.Place:
                    return s.Step == BuildStep.Ghost ? "reallife_bld_hint_extra_ghost" : "reallife_bld_hint_extra_zoned";
                case BuildTool.Bulldoze:
                case BuildTool.Refurbish:
                    return "reallife_bld_hint_extra_yours";
                default:
                    return null;
            }
        }

        /// <summary>What Esc does now, said beside the hint: the question box's own while it is up, a design let go, the squares cleared, else leaving.</summary>
        public static string Esc(BuildModeState s, bool picked)
        {
            if (!s.Open)
            {
                return null;
            }
            if (s.Step == BuildStep.Confirming)
            {
                return "reallife_bld_hint_esc_answer";
            }
            if (s.Step == BuildStep.Ghost)
            {
                return "reallife_bld_hint_esc_design";
            }
            return picked || s.Step == BuildStep.Dragging ? "reallife_bld_hint_esc_clear" : "reallife_bld_hint_esc_leave";
        }

        /// <summary>The tour's step after this one; skipped: done.</summary>
        public static TourStep TourNext(TourStep step, bool skip)
        {
            if (skip || step >= TourStep.Demand || step < TourStep.HintBar)
            {
                return TourStep.Done;
            }
            return step + 1;
        }

        /// <summary>The tour's headings, by TourStep.</summary>
        public static readonly string[] TourTitles =
        {
            "reallife_bld_tour_hint_title", "reallife_bld_tour_tools_title", "reallife_bld_tour_views_title", "reallife_bld_tour_demand_title"
        };

        /// <summary>The tour's words under each heading, by TourStep.</summary>
        public static readonly string[] TourBodies =
        {
            "reallife_bld_tour_hint_body", "reallife_bld_tour_tools_body", "reallife_bld_tour_views_body", "reallife_bld_tour_demand_body"
        };

        /// <summary>True when the tour is shown at this opening: never seen before, and not in a Going Public session (nothing there to do).</summary>
        public static bool TourWanted(bool seen, bool viewOnly) => !seen && !viewOnly;

        public const int DemandLow = BuildModeRules.DeveloperDemandLeast;   // under this the city's developers build none of the use
        public const int DemandHigh = 65;                                   // from this the use is wanted a lot

        /// <summary>A demand bar's words for its tooltip: not known yet, little wanted (the developers wait), wanted, wanted a lot.</summary>
        public static string DemandWords(int demand)
        {
            if (demand < 0)
            {
                return "reallife_bld_tip_demand_unknown";
            }
            if (demand < DemandLow)
            {
                return "reallife_bld_tip_demand_low";
            }
            return demand < DemandHigh ? "reallife_bld_tip_demand_some" : "reallife_bld_tip_demand_high";
        }

        /// <summary>A demand bar's fill, 0 to 1 (none known: empty).</summary>
        public static float DemandFill(int demand) => demand <= 0 ? 0f : demand >= 100 ? 1f : demand / 100f;

        // ------------------------------------------------------------------ the owner's test of the interface (26 Oct 2026: "Fix those")

        /// <summary>What a forgiving drag left out, in words for the tool panel (CellRules.Took*), or null for nothing to say.</summary>
        public static string TookKey(BuildTool tool, int took)
        {
            switch (took)
            {
                case CellRules.TookWide: return "reallife_bld_took_wide";
                case CellRules.TookGap: return "reallife_bld_took_gap";
                case CellRules.TookStreet: return "reallife_bld_took_street";
                case CellRules.TookDeeper: return "reallife_bld_took_deeper";
                case CellRules.TookTaken:
                    return tool == BuildTool.Land ? "reallife_bld_took_taken_land" : tool == BuildTool.Bulldoze || tool == BuildTool.Refurbish ? null : "reallife_bld_took_taken_mine";
                default: return null;
            }
        }

        /// <summary>How to put right squares that make no site or cannot be bought, in plain words under the reason; null where the reason says it.</summary>
        public static string FixKey(string why)
        {
            switch (why)
            {
                case CellRules.TwoStrips: return "reallife_bld_fix_strips";
                case CellRules.NotJoined: return "reallife_bld_fix_joined";
                case CellRules.TooWide: return "reallife_bld_fix_wide";
                case CellRules.TooFew: return "reallife_bld_fix_few";
                case "reallife_bld_mode_refuse_owned": return "reallife_bld_fix_owned";
                case "reallife_bld_answer_lot_taken": return "reallife_bld_fix_taken";
                case "reallife_bld_mode_refuse_mixed": return "reallife_bld_fix_mixed";
                case "reallife_bld_mode_refuse_two_sites": return "reallife_bld_fix_two_sites";
                default: return null;
            }
        }

        /// <summary>
        /// The hint bar's two lines in one row of <paramref name="room"/> (the canvas's units): both as wide as their words when they fit;
        /// else the quieter line wraps first (to no less than a quarter of the room), and only then the main one. -1: as wide as its words.
        /// </summary>
        public static void HintFit(float main, float more, float room, out float mainCap, out float moreCap)
        {
            mainCap = moreCap = -1f;
            if (room <= 0f)
            {
                return;
            }
            if (more <= 0f)
            {
                mainCap = main > room ? room : -1f;
                return;
            }
            if (main + more <= room)
            {
                return;
            }
            float least = Math.Min(more, room / 4f);
            if (main + least <= room)
            {
                moreCap = room - main;
                return;
            }
            moreCap = least < more ? least : -1f;
            mainCap = room - least;
        }

        /// <summary>
        /// Where a tour's card stands (the owner, 26 Oct 2026: it must not cover "the thing it points at or the tool panel"): on the
        /// preferred side of its part (under the hint bar, over a part at the foot), else the other side, else beside it; along that
        /// side centred on the part, else just past each part in the way. The first place on the screen that meets neither the part
        /// nor any part in <paramref name="avoid"/>; false (the card centred on the screen) when none. Game-free; no allocation.
        /// </summary>
        public static bool TourCard(UiBox screen, UiBox part, UiBox[] avoid, int avoidCount, float wide, float high, float gap, bool below, out UiBox card)
        {
            for (int side = 0; side < 4; side++)
            {
                int which = side < 2 ? (below ? side : 1 - side) : side;   // 0 under, 1 over, 2 right, 3 left
                for (int k = -1; k < avoidCount + 1; k++)
                {
                    for (int end = 0; end < 2; end++)
                    {
                        if (k < 0 && end > 0)
                        {
                            continue;   // centred is one place
                        }
                        UiBox by = k < 0 ? part : k < avoidCount ? avoid[k] : part;
                        float x0, y0;
                        if (which < 2)
                        {
                            y0 = which == 0 ? part.Y0 - gap - high : part.Y1 + gap;
                            x0 = k < 0 ? (part.X0 + part.X1 - wide) * 0.5f : end == 0 ? by.X1 + gap : by.X0 - gap - wide;
                        }
                        else
                        {
                            x0 = which == 2 ? part.X1 + gap : part.X0 - gap - wide;
                            y0 = k < 0 ? (part.Y0 + part.Y1 - high) * 0.5f : end == 0 ? by.Y1 + gap : by.Y0 - gap - high;
                        }
                        x0 = Math.Max(screen.X0 + gap, Math.Min(x0, screen.X1 - gap - wide));
                        y0 = Math.Max(screen.Y0 + gap, Math.Min(y0, screen.Y1 - gap - high));
                        var at = new UiBox(x0, y0, x0 + wide, y0 + high);
                        if (Clear(at, part, avoid, avoidCount) && at.Inside(screen))
                        {
                            card = at;
                            return true;
                        }
                    }
                }
            }
            float cx = (screen.X0 + screen.X1 - wide) * 0.5f, cy = (screen.Y0 + screen.Y1 - high) * 0.5f;
            card = new UiBox(cx, cy, cx + wide, cy + high);
            return false;
        }

        private static bool Clear(UiBox at, UiBox part, UiBox[] avoid, int avoidCount)
        {
            if (at.Meets(part))
            {
                return false;
            }
            for (int i = 0; i < avoidCount && avoid != null && i < avoid.Length; i++)
            {
                if (at.Meets(avoid[i]))
                {
                    return false;
                }
            }
            return true;
        }
    }

    /// <summary>A rectangle of the interface (x to the right, y upwards), for the game-free layout rules.</summary>
    public struct UiBox
    {
        public float X0, Y0, X1, Y1;

        public UiBox(float x0, float y0, float x1, float y1)
        {
            X0 = x0;
            Y0 = y0;
            X1 = x1;
            Y1 = y1;
        }

        public float Wide => X1 - X0;

        public float High => Y1 - Y0;

        /// <summary>True when the two share any area (touching edges do not).</summary>
        public bool Meets(UiBox o) => X0 < o.X1 && o.X0 < X1 && Y0 < o.Y1 && o.Y0 < Y1;

        /// <summary>True when this lies inside <paramref name="o"/> (half a unit's slack).</summary>
        public bool Inside(UiBox o) => X0 >= o.X0 - 0.5f && X1 <= o.X1 + 0.5f && Y0 >= o.Y0 - 0.5f && Y1 <= o.Y1 + 0.5f;
    }
}
