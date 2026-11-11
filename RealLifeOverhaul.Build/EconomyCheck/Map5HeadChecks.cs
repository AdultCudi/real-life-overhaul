using System;
using System.Text.RegularExpressions;
using RealLifeOverhaul.Nav;

// The minimap's headline and its district strip (the owner's report of 12 Nov: at 1920 x 1080 with a route set, the band stood light, the minutes and
// the distance were white on it and ran past the card's right edge, the address was drawn under the band and half outside the card, the name was cut
// with room to spare, and the round buttons crowded the district strip). The band's layout and colours are NavHeadRules, laid out here at every card
// size and with the longest minutes of every language; the sources are scanned for using them. Part of `--map5`.
internal static partial class Program
{
    private static void Map5HeadChecks()
    {
        float head = NavPlaceRules.Head;
        float bandHeight = NavHeadRules.BandHeight(head);

        // The band stands inside the card's drawn edge and above the map, tall enough for the name over the address.
        Check(NavHeadRules.Inset >= NavHeadRules.Drawn && NavHeadRules.BandGap >= 0f && NavHeadRules.Inset + bandHeight + NavHeadRules.BandGap <= head + 0.01f && bandHeight >= 70f,
            "minimap head: the band is inside the card's drawn edge (" + NavHeadRules.Inset + " of " + NavHeadRules.Drawn + "), above the map, and " + bandHeight + " high for two rows");

        // Every text reads against the band: the band nearly solid, over the brightest street and the darkest.
        uint overWhite = NavHeadRules.Over(NavHeadRules.Band, 0xFFFFFFFF), overBlack = NavHeadRules.Over(NavHeadRules.Band, 0x000000FF);
        string faint = null;
        foreach (var (name, colour) in new[] { ("name", NavHeadRules.Name), ("address", NavHeadRules.Address), ("minutes", NavHeadRules.Minutes), ("distance", NavHeadRules.Distance) })
        {
            double worst = Math.Min(NavHeadRules.Contrast(colour, overWhite), NavHeadRules.Contrast(colour, overBlack));
            if (worst < 4.5 && faint == null)
            {
                faint = name + " at " + worst.ToString("0.0");
            }
        }
        Check(faint == null, "minimap head: every text reads against the band at 4.5 to 1 or better over a white or a black street (" + (faint ?? "all clear") + ")");
        Check((NavHeadRules.Band & 0xFF) >= 0xE0, "minimap head: the band is nearly solid, so the street under it never decides how the texts read");
        Check(NavHeadRules.DistanceHex == "#" + (NavHeadRules.Distance >> 8).ToString("X6"), "minimap head: the distance's colour in the joined text is the rules' distance colour");

        // The texts stay inside the band at every card size, with or without an address and minutes, and the name keeps room beside the minutes.
        string outside = null;
        float leastName = float.MaxValue;
        float[] wants = { 0f, 40f, 90f, 150f, 230f, 600f };
        for (int percent = 50; percent <= 100; percent += 5)
        {
            float card = NavPlaceRules.Width(percent);
            float width = NavHeadRules.BandWidth(card);
            foreach (float want in wants)
            {
                for (int parts = 0; parts < 4; parts++)
                {
                    bool address = (parts & 1) != 0, right = (parts & 2) != 0;
                    NavHeadRules.Layout(width, bandHeight, want, address, right, out NavHeadBox name, out NavHeadBox under, out NavHeadBox minutes);
                    string where = percent + "% card, minutes asking " + want + (address ? ", address" : "") + (right ? ", minutes" : "");
                    if (outside == null && (!Inside(name, width, bandHeight) || (address && !Inside(under, width, bandHeight)) || (right && !Inside(minutes, width, bandHeight))))
                    {
                        outside = "a text outside the band at " + where;
                    }
                    if (outside == null && right && (name.Right + NavHeadRules.Between > minutes.X + 0.01f || (address && under.Right + NavHeadRules.Between > minutes.X + 0.01f)))
                    {
                        outside = "the name or the address runs under the minutes at " + where;
                    }
                    if (outside == null && address && under.Top > name.Y + 0.01f)
                    {
                        outside = "the address is not under the name at " + where;
                    }
                    if (outside == null && right && want <= (width - NavHeadRules.Pad * 2f) * NavHeadRules.RightMost && minutes.Width < want - 0.01f)
                    {
                        outside = "the minutes get less than they ask at " + where;
                    }
                    leastName = Math.Min(leastName, name.Width);
                }
            }
        }
        Check(outside == null, "minimap head: the name, the address and the minutes stay inside the band at every card size (" + (outside ?? "all inside") + ")");
        Check(leastName >= 120f, "minimap head: the name keeps at least 120 units beside the longest minutes at the smallest card (" + leastName + ")");

        // The longest minutes of every language (16 h 39 at the most) fit their column at the least lettering on the smallest card: German and Russian too.
        string root = Map5Root();
        string cut = null;
        float smallest = NavHeadRules.BandWidth(NavPlaceRules.Width(50));
        foreach (string file in System.IO.Directory.GetFiles(System.IO.Path.Combine(root, "Assets", "Mods", "RealLifeOverhaul", "Locales"), "*.json"))
        {
            string json = System.IO.File.ReadAllText(file);
            Match hours = Regex.Match(json, "\"reallife_nav_hours\"\\s*:\\s*\"([^\"]*)\"");
            if (!hours.Success)
            {
                continue;
            }
            string words = hours.Groups[1].Value.Replace("{hours}", "16").Replace("{minutes}", "39");
            float wanted = 0f;
            foreach (char letter in words)
            {
                wanted += (letter >= (char)0x2E80 ? 1f : 0.6f) * NavHeadRules.RightSize;   // a generous width per letter, bold: a full square for Chinese and Korean
            }
            NavHeadRules.Layout(smallest, bandHeight, wanted, true, true, out _, out _, out NavHeadBox minutes);
            if (wanted * NavHeadRules.RightLeastSize / NavHeadRules.RightSize > minutes.Width + 0.01f && cut == null)
            {
                cut = System.IO.Path.GetFileNameWithoutExtension(file) + " '" + words + "' needs " + Math.Round(wanted * NavHeadRules.RightLeastSize / NavHeadRules.RightSize) + " of " + minutes.Width;
            }
        }
        Check(cut == null, "minimap head: the longest minutes of every language fit their column whole on the smallest card (" + (cut ?? "all whole") + ")");

        // The destination's marker, held at the view's edge, slides clear of the district strip and the round buttons, along its own line.
        string crowded = null;
        const float strip = 14f + 46f, compass = 52f, gap = 10f, pad = 14f, radius = 29f;
        foreach (float viewWidth in new[] { 306f, 400f, 528f, 676f })
        {
            foreach (float viewHeight in new[] { 240f, 306f, 528f })
            {
                float halfWidth = viewWidth * 0.5f, halfHeight = viewHeight * 0.5f;
                for (int degrees = 0; degrees < 360; degrees += 3)
                {
                    double a = degrees * Math.PI / 180.0;
                    float x = (float)Math.Cos(a) * 1000f, y = (float)Math.Sin(a) * 1000f;
                    float over = Math.Max(Math.Abs(x) / (halfWidth - radius), Math.Abs(y) / (halfHeight - radius));
                    x /= over;
                    y /= over;
                    float x0 = x, y0 = y;
                    NavHeadRules.KeepClear(ref x, ref y, halfWidth, halfHeight, strip + radius, pad + compass + radius, pad + compass * 2f + gap + radius);
                    bool inRow = y + radius > halfHeight - strip + 0.01f;
                    bool inColumn = x + radius > halfWidth - pad - compass + 0.01f && y + radius > halfHeight - pad - compass * 2f - gap + 0.01f;
                    bool turned = Math.Abs(x * y0 - y * x0) > 0.5f || x * x0 < -0.01f || y * y0 < -0.01f;
                    if ((inRow || inColumn || turned) && crowded == null)
                    {
                        crowded = degrees + " degrees in a " + viewWidth + " by " + viewHeight + " view: " + (turned ? "turned off its line" : inRow ? "under the strip" : "under the buttons");
                    }
                }
            }
        }
        Check(crowded == null, "minimap: the destination's marker at the view's edge stays clear of the district strip and the round buttons and still points the way (" + (crowded ?? "all clear") + ")");

        // The sources: the band is the mod's own and its texts take their place and colours from the rules; the strip fits beside the buttons.
        string make = Map5Source("Navigation/NavHud.Make.cs"), dest = Map5Source("Navigation/NavHud.Destination.cs");
        string hud = Map5Source("Navigation/NavHud.cs"), stripSource = Map5Source("Navigation/NavHud.Strip.cs");
        Check(make.Contains("bandImage.color = Colour(NavHeadRules.Band)") && make.Contains("gameBand.enabled = false") && make.Contains("titleText.color = Colour(NavHeadRules.Name)")
              && make.Contains("Take<LayoutGroup>(_head.gameObject)") && make.Contains("NavHeadRules.Inset"),
            "minimap head: the game's light headline picture and its layout go; the band is the card's own plate inside the drawn edge, the name in the accent");
        Check(dest.Contains("NavHeadRules.Layout(") && dest.Contains("HeadLabel(\"Address\", NavHeadRules.Address") && dest.Contains("HeadLabel(\"Minutes\", NavHeadRules.Minutes")
              && !dest.Contains("_titleOffMin") && !dest.Contains("new Color32(233, 239, 244, 255)") && hud.Contains("HeadLayout();"),
            "minimap head: the name, the address and the minutes are placed by NavHeadRules in the band, in its colours, and laid out again when the card's width changes");
        Check(hud.Contains("NavHeadRules.KeepClear(") && stripSource.Contains("ViewWidth - CompassSize - StripPad * 3f") && stripSource.Contains("_stripText.enableAutoSizing = true")
              && stripSource.Contains("StripFit();") && hud.Contains("StripFit();"),
            "minimap: the district strip fits beside the compass and the switch at every width, its lettering shrinks before the street is shortened, and the marker keeps clear");
    }

    private static bool Inside(NavHeadBox box, float width, float height)
    {
        return box.X >= -0.01f && box.Y >= -0.01f && box.Right <= width + 0.01f && box.Top <= height + 0.01f && box.Width >= 0f && box.Height > 0f;
    }
}
