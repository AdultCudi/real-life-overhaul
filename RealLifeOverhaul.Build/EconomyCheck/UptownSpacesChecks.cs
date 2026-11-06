using System;
using System.Collections.Generic;
using RealLifeOverhaul.Construction;
using RealLifeOverhaul.NewBuildings;
using RealLifeOverhaul.Uptown;

// Uptown's grid of spaces (Scripts/Uptown/SpacesRules.cs) against the game's own designs (Census/design-fronts-3682.json):
// each structure's frontage and depth the medians of the game's buildings of it that the copy engine can copy, its spaces
// that frontage rounded up to whole spaces of A1's, the narrowest, to the whole metre; the strips cut into whole spaces;
// and the construction game's rule of what a site offers, the same as the district's.
internal static partial class Program
{
    private static void UptownSpacesChecks()
    {
        List<FrontDesign> fixture = UptownPlaceFixture();
        if (fixture == null)
        {
            Check(false, "uptown spaces: the game's designs (" + DesignFronts + ") could not be read");
            return;
        }
        var designs = new List<DesignEntry>(fixture.Count);
        foreach (FrontDesign f in fixture)
        {
            designs.Add(f.Now);
        }
        string worst = null;
        float off = 0f;
        int measured = 0;
        foreach (SpaceSize s in SpacesRules.Grid)
        {
            var fronts = new List<float>();
            var deeps = new List<float>();
            foreach (DesignEntry d in designs)
            {
                if (d.Size == s.Size && d.Version == s.Version && NewBuildingsRules.Copyable(d) && !d.Whole)
                {
                    fronts.Add(FrontageRules.Across(d));
                    deeps.Add(FrontageRules.Deep(d));
                }
            }
            if (fronts.Count == 0)
            {
                continue;   // H3, R2, R3 and the cinemas have no building of their own the engine can copy: R1's front, the table's
            }
            measured++;
            fronts.Sort();
            deeps.Sort();
            float front = Median(fronts), deep = Median(deeps), miss = Math.Max(Math.Abs(front - s.Front), Math.Abs(deep - s.Deep));
            if (miss > off)
            {
                off = miss;
                worst = s.Name + " " + front.ToString("F1") + " by " + deep.ToString("F1") + " m";
            }
        }
        Check(measured >= 20 && off <= 0.6f,
            "uptown spaces: each structure's frontage and depth in the grid are the medians of the game's own buildings of it the copy engine can copy, within 0.6 m (" + measured
            + " measured, the furthest " + off.ToString("F2") + " m: " + worst + ")");

        float a1 = SpacesRules.Of("ba:buildingsize_a", 1).Front;
        bool narrowest = true, counted = true;
        foreach (SpaceSize s in SpacesRules.Grid)
        {
            narrowest &= s.Front >= a1;
            counted &= s.Spaces == (int)Math.Ceiling(s.Front / SpacesRules.SpaceWidth) && s.Spaces >= 1 && s.DepthClass <= SpacesRules.AvenueDepth;
        }
        Check(narrowest && SpacesRules.SpaceWidth == (int)Math.Ceiling(a1) && counted && SpacesRules.SpacesOf("ba:buildingsize_k", 1) == 5 && SpacesRules.SpacesOf("ba:buildingsize_t", 1) == 0
              && SpacesRules.SpacesOf("ba:buildingsize_boat", 1) == 0 && SpacesRules.Of("ba:buildingsize_q", 2) == null,
            "uptown spaces: one space is A1's frontage, the narrowest, to the whole metre (" + SpacesRules.SpaceWidth + " m); every structure takes its frontage rounded up to whole spaces"
            + " and fits a strip; the boat's, parking's, T's and the too deep warehouses are off the grid");

        var widths = new List<int>();
        float left = SpacesRules.Cut(75.4f, widths);
        int sum = 0;
        foreach (int w in widths)
        {
            sum += w;
        }
        Check(widths.Count == 10 && sum == 75 && Math.Abs(left - 0.4f) < 0.01f && widths[0] == 8 && widths[9] == 8 && widths[1] == 8 && widths[5] == 7
              && SpacesRules.Cut(6f, widths) == 6f && widths.Count == 0 && SpacesRules.Cut(70f - 0.00001f, widths) < 0.01f && widths.Count == 10,
            "uptown spaces: a strip is cut into spaces of 7 m, its whole metres over one each to its spaces from its two ends in turn, under a metre left at its ends");

        bool same = true;
        for (int site = 1; site <= 7; site++)
        {
            for (int size = 0; size <= 8; size++)
            {
                same &= BuildCatalogue.Offered(site, size, true) == SpacesRules.Offered(site, size, true) && BuildCatalogue.Offered(site, size, false) == SpacesRules.Offered(site, size, false);
            }
        }
        Check(same && BuildCatalogue.SiteSliver == SpacesRules.FillSliver && BuildCatalogue.SiteSpacesMost >= SpacesRules.SpacesOf("ba:buildingsize_p", 1)
              && SpacesRules.Offered(3, 3, false) && !SpacesRules.Offered(3, 2, false) && SpacesRules.Offered(3, 2, true) && !SpacesRules.Offered(2, 3, true) && !SpacesRules.Offered(2, 0, true),
            "uptown spaces: a site offers a size of exactly its spaces, or fewer only when the design fills it alone; the construction game and the district keep one rule");
        Check(SpacesRules.Fills(6.3f, 7f) && SpacesRules.Fills(7f, 7f) && !SpacesRules.Fills(5.9f, 7f) && !SpacesRules.Fills(7.2f, 7f) && SpacesRules.Doors("ba:buildingsize_m") == 2
              && SpacesRules.Doors("ba:buildingsize_c") == 1,
            "uptown spaces: a look fills its spaces within a metre, never wider; the game's M, R and S have two ways in");
    }

    private static float Median(List<float> sorted)
    {
        int n = sorted.Count;
        return n % 2 == 1 ? sorted[n / 2] : (sorted[n / 2 - 1] + sorted[n / 2]) * 0.5f;
    }
}
