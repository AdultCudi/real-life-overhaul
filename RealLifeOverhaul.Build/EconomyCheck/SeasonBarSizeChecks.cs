using System;
using System.Globalization;
using System.Text.RegularExpressions;
using RealLifeOverhaul.Seasons;

// The season under the date's size option (Scripts/Seasons/SeasonRules.BarScale and SeasonBar): Normal is the first look
// to the pixel, the steps grow, a value outside them is taken as the nearest, the largest still fits beside the clock
// inside the top bar's left part (the game's own sizes, read from its UI scene), and the option is applied at the bar's
// once-a-second call by one comparison, laying the rects again without building anything.
internal static partial class Program
{
    // From the game's UI scene (Canvases/Topbar/Container/Left Bar, build 3682): the left part is 3,191.27 wide; the date's label
    // starts 2,230.6 from its left edge and 133.2 down; the clock's box spans 2,695.6 to 2,975.6 and ends 133.2 down. The piece
    // hangs Below under the label's lower edge, so the clock and the date are above it at every size.
    private const float BarLeftPart = 3191.27f;    // top bar units
    private const float DateLabelLeft = 2230.6f;   // top bar units, from the left part's left edge

    private static void SeasonBarSize()
    {
        Check(SeasonRules.BarScale(SeasonRules.BarNormal) == 1f, "season bar size: Normal is exactly the first size, so the picture, words and box stay as they were");
        Check(SeasonRules.BarScale(SeasonRules.BarNormal) < SeasonRules.BarScale(SeasonRules.BarLarge)
              && SeasonRules.BarScale(SeasonRules.BarLarge) < SeasonRules.BarScale(SeasonRules.BarLargest)
              && Math.Abs(SeasonRules.BarScale(SeasonRules.BarLargest) - 2f) < 0.001f,
            "season bar size: each step is larger than the one before, and Largest is twice the first size");
        Check(SeasonRules.BarSizeOf(-1) == SeasonRules.BarNormal && SeasonRules.BarSizeOf(9) == SeasonRules.BarLargest && SeasonRules.BarScale(-5) == 1f && SeasonRules.BarScale(int.MaxValue) == 2f,
            "season bar size: a value outside the steps (a hand-edited options file) is taken as the nearest step, never an error");

        string bar = ModSource(@"Seasons\SeasonBar.cs");
        float width = SourceFloat(bar, "Width"), picture = SourceFloat(bar, "Picture"), gap = SourceFloat(bar, "Gap");
        float largest = SeasonRules.BarScale(SeasonRules.BarLargest);
        Check(width > 0f && DateLabelLeft + width * largest <= BarLeftPart,
            "season bar size: at Largest the piece ends " + (DateLabelLeft + width * largest).ToString("F0", CultureInfo.InvariantCulture)
            + " from the top bar's left edge, inside its left part (" + BarLeftPart.ToString("F0", CultureInfo.InvariantCulture) + "), where the game's notifications do not reach");
        Check(picture == 28f && gap == 8f && width == 420f && SourceFloat(bar, "Height") == 34f && SourceFloat(bar, "Shrink") == 0.8f && SourceFloat(bar, "Below") == 4f,
            "season bar size: the first sizes are the ones the piece always had (picture 28, gap 8, box 420 x 34, words 0.8 of the date's, 4 below it)");

        string show = MethodText(bar, "public static void Show(string line, int season)");
        string lay = MethodText(bar, "private static void Lay(int size)");
        string build = MethodText(bar, "private static bool Build()");
        Check(show.Contains("int size = Settings.SeasonBarSize;") && show.Contains("if (size != _size)") && show.Contains("Lay(size);"),
            "season bar size: the option is applied at the bar's once-a-second call by one comparison, without building the piece again");
        Check(lay.Contains("SeasonRules.BarScale(size)") && lay.Contains("_rect.sizeDelta = new Vector2(Width * scale, Height * scale);") && lay.Contains("_pictureRect.sizeDelta = new Vector2(picture, picture);")
              && lay.Contains("_wordsRect.offsetMin = new Vector2(picture + Gap * scale, 0f);") && lay.Contains("_text.fontSize = _dateSize * Shrink * scale;") && lay.Contains("Meta.Dev.Verbose"),
            "season bar size: the box, the picture, the words' start and their font are laid from one scale, with a self-check line under diagnostics");
        Check(!build.Contains("sizeDelta = new Vector2(Width") && !build.Contains("fontSize =") && build.Contains("_size = -1;") && bar.Contains("Seasons check (top bar size): "),
            "season bar size: the build leaves the sizes to the first lay-out, so there is one place they are set");
        Check(MethodText(bar, "public static void Remove()").Contains("_size = -1;"), "season bar size: a new city lays the piece out again at the option's size");

        string settings = ModSource(@"Core\Settings.cs");
        string guide = ModSource(@"Seasons\SeasonGuide.cs");
        string tree = ModSource(@"Meta\OptionsTree.cs");
        Check(settings.Contains("public static int SeasonBarSize = 0;") && settings.Contains("public const string SeasonBarSizeId = \"season_bar_size\";")
              && settings.Contains("SeasonBarSize = System.Math.Max(0, System.Math.Min(2, Int(modId, SeasonBarSizeId, SeasonBarSize)));"),
            "season bar size: the option is Normal by default and read within its three steps");
        int toggle = guide.IndexOf(".AddToggle(Settings.SeasonBarId,", StringComparison.Ordinal), dropdown = guide.IndexOf(".AddDropdown(Settings.SeasonBarSizeId,", StringComparison.Ordinal);
        int coats = guide.IndexOf(".AddToggle(Settings.SeasonCoatsId,", StringComparison.Ordinal);
        Check(toggle > 0 && dropdown > toggle && coats > dropdown && guide.Contains("Settings.SeasonBarId, Settings.SeasonBarSizeId,")
              && tree.Contains(".Opt(\"season_bar_size\", \"season_bar\")"),
            "season bar size: the dropdown stands right under the season under the date's toggle, belongs to the seasons' Guide page and needs that toggle on");
        foreach (string code in new[] { "en", "pt", "zh-cn" })
        {
            var texts = LocaleFile(code);
            bool all = true;
            foreach (string key in new[] { "reallife_season_bar_size_option", "reallife_season_bar_size_normal", "reallife_season_bar_size_large", "reallife_season_bar_size_largest", "reallife_opt_tip_season_bar_size" })
            {
                all &= texts.TryGetValue(key, out string text) && text.Length > 0;
            }
            Check(all && texts.TryGetValue("reallife_guide_seasons", out string page) && page.Contains("<b>" + texts["reallife_season_bar_size_option"] + "</b>"),
                "season bar size: the option, its three steps and its tip are in " + code + ", and the seasons' Guide page names it");
        }
    }

    // A float constant of a source, by name (private const float Name = 1.5f;); NaN if it is not there.
    private static float SourceFloat(string source, string name)
    {
        Match m = Regex.Match(source, @"private const float " + name + @" = ([0-9.]+)f;");
        return m.Success ? float.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture) : float.NaN;
    }
}
