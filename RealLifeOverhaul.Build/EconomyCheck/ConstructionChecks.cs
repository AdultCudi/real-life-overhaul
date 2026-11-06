using System;
using System.Collections.Generic;
using System.IO;
using RealLifeOverhaul;
using IoPath = System.IO.Path;

// The construction programme's new kinds (Scripts/Civic): a rebuild of a whole building and a new building on given ground.
// Their records in the save and in the line a Going Public host sends keep the shape they had (the same fields, the plot's
// ground in the street field as text), an older reading clamps a kind it does not know, the catalogue has a name and a
// stage name for each, and every text the tests and the stages show is in the three languages.
internal static partial class Program
{
    private static void Construction()
    {
        ConstructionCatalogue();
        ConstructionSave();
        ConstructionShare();
        ConstructionTexts();
    }

    private static void ConstructionCatalogue()
    {
        int kinds = Enum.GetValues(typeof(SiteKind)).Length;
        Check(kinds == 8 && (int)SiteKind.Rebuild == 4 && (int)SiteKind.Plot == 5 && (int)SiteKind.Demolition == 6 && (int)SiteKind.Parking == 7,
            "construction: the rebuild, the plot, the demolition and the car park are kinds 4, 5, 6 and 7, after the four the saves already hold");
        Check(CivicCatalogue.Projects.Length == kinds * 3 && CivicCatalogue.Phases.Length == kinds * CivicCatalogue.PhasesPerKind && CivicCatalogue.SiteHours.Length == kinds,
            "construction: three project names, five stage names and the working hours for every kind of site");
        Check(CivicCatalogue.SiteHours[(int)SiteKind.Rebuild] > CivicCatalogue.SiteHours[(int)SiteKind.Scaffold] && CivicCatalogue.SiteHours[(int)SiteKind.Plot] > CivicCatalogue.SiteHours[(int)SiteKind.Rebuild],
            "construction: a rebuild takes longer than a facade's scaffold, and a new building longer than a rebuild");
        Check(CivicCatalogue.MostRebuilds == 1, "construction: one wrapped building in the city at a time");
        var plot = new SiteState { Kind = SiteKind.Plot, Needed = 100f, UnsafeFrom = -1f };
        var rebuild = new SiteState { Kind = SiteKind.Rebuild, Needed = 100f, UnsafeFrom = -1f };
        bool noisy = true;
        for (int phase = 0; phase < 5; phase++)
        {
            plot.Done = rebuild.Done = phase * 20f + 1f;
            noisy &= plot.Noisy == (phase >= 1 && phase <= 3) && rebuild.Noisy == (phase == 1 || phase == 2 || phase == 4);
        }
        Check(noisy, "construction: the dig, the slab and the frame are heard next door, and the scaffold going up, the works and its coming down");
    }

    private static void ConstructionSave()
    {
        var state = new WorksState { NextId = 9, LastHour = 4000 };
        state.Sites.Add(new SiteState { Id = 7, Kind = SiteKind.Rebuild, Street = "ba:street_fifthavenue", Number = 36, X = 1f, Y = 2f, Z = 3f, Needed = 160f, Done = 40f, UnsafeFrom = -1f });
        state.Sites.Add(new SiteState { Id = 8, Kind = SiteKind.Plot, Street = "plot:24x20:090:5", Number = 0, X = -12.5f, Y = 0.25f, Z = 7f, Needed = 320f, Done = 200f, UnsafeFrom = -1f, Local = true });
        string line = state.Write();
        var back = new WorksState();
        Check(back.Read(line) && back.Sites.Count == 2 && back.Sites[0].Kind == SiteKind.Rebuild && back.Sites[1].Kind == SiteKind.Plot
              && back.Sites[1].Street == "plot:24x20:090:5" && back.Sites[1].X == -12.5f && back.Sites[1].Done == 200f && back.Write() == line,
            "construction: a rebuild and a plot come back from the save as they went in, the plot's ground in its street field");
        Check(back.Sites[1].Street.IndexOf('|') < 0, "construction: a plot's record holds no divider of the save's line");
        // A kind of a later version reads as the last this one knows; a negative one as roadworks: the save always loads.
        string later = line.Replace("|8|5|plot:", "|8|9|plot:");
        var older = new WorksState();
        Check(older.Read(later) && older.Sites[1].Kind == SiteKind.Parking, "construction: a site of a kind this version does not know is read as its last kind");
        Check(Fields(new SiteState { Kind = SiteKind.Plot, Street = "plot:24x20:090:5", UnsafeFrom = -1f }) == Fields(new SiteState { Kind = SiteKind.Scaffold, Street = "ba:street_x", UnsafeFrom = -1f }),
            "construction: a plot's record has the same fields as any site's (nothing added to the save's line)");
    }

    // The number of fields one site writes.
    private static int Fields(SiteState site)
    {
        var w = new CivicWriter();
        site.Write(w);
        return w.ToString().Split('|').Length;
    }

    private static void ConstructionShare()
    {
        var host = new WorksState { NextId = 12 };
        host.Sites.Add(new SiteState { Id = 10, Kind = SiteKind.Rebuild, Street = "ba:street_fifthavenue", Number = 36, Needed = 160f, Done = 80f, UnsafeFrom = -1f });
        host.Sites.Add(new SiteState { Id = 11, Kind = SiteKind.Plot, Street = "plot:30x22:180:6", Needed = 320f, Done = 10f, UnsafeFrom = -1f });
        string sent = CivicShare.WriteWorks(host);
        var guest = new WorksState();
        Check(CivicShare.ApplyWorks(guest, sent) && guest.Sites.Count == 2 && guest.Sites[0].Kind == SiteKind.Rebuild && guest.Sites[1].Kind == SiteKind.Plot
              && guest.Sites[1].Street == "plot:30x22:180:6" && CivicShare.WriteWorks(guest) == sent,
            "construction: a guest of a Going Public session takes the host's rebuild and plot as they are, and would send the same line back");
        // The line keeps its shape: the version and the fields of each site as before the new kinds.
        var plain = new WorksState { NextId = 12 };
        plain.Sites.Add(new SiteState { Id = 10, Kind = SiteKind.Scaffold, Street = "ba:street_fifthavenue", Number = 36, Needed = 160f, Done = 80f, UnsafeFrom = -1f });
        plain.Sites.Add(new SiteState { Id = 11, Kind = SiteKind.Utility, Street = "ba:street_x", Needed = 320f, Done = 10f, UnsafeFrom = -1f });
        Check(CivicShare.Version == 1 && sent.Split('|').Length == CivicShare.WriteWorks(plain).Split('|').Length,
            "construction: the host's line of sites keeps its version and its number of fields (nothing new is sent)");
    }

    private static readonly string[] ConstructionKeys =
    {
        "reallife_construction_plot_place", "reallife_construction_test_wrap", "reallife_construction_test_rebuild", "reallife_construction_test_stage",
        "reallife_construction_test_storey", "reallife_construction_test_crane", "reallife_construction_test_wrap_done", "reallife_construction_test_wrap_refused",
        "reallife_construction_test_no_site", "reallife_construction_test_advanced", "reallife_construction_test_handed", "reallife_construction_test_no_ground",
        "reallife_construction_test_storey_done", "reallife_construction_test_crane_done", "reallife_construction_test_crane_refused", "reallife_contract_part_site_wrap_fade"
    };

    private static void ConstructionTexts()
    {
        string folder = LocalesFolder();
        var lost = new List<string>();
        foreach (string code in new[] { "en", "pt", "zh-cn" })
        {
            Dictionary<string, string> words = LoadWords(IoPath.Combine(folder, code + ".json"));
            if (words == null)
            {
                lost.Add(code + ".json");
                continue;
            }
            var keys = new List<string>(ConstructionKeys);
            for (int i = (int)SiteKind.Rebuild * 3; i < CivicCatalogue.Projects.Length; i++)
            {
                keys.Add(CivicCatalogue.Projects[i]);
            }
            for (int i = (int)SiteKind.Rebuild * CivicCatalogue.PhasesPerKind; i < CivicCatalogue.Phases.Length; i++)
            {
                keys.Add(CivicCatalogue.Phases[i]);
            }
            foreach (string key in keys)
            {
                if (!words.TryGetValue(key, out string value) || string.IsNullOrEmpty(value))
                {
                    lost.Add(code + ":" + key);
                }
            }
        }
        Check(lost.Count == 0, "construction: every text of the new kinds and their tests is in en, pt and zh-cn" + (lost.Count > 0 ? " (missing: " + string.Join(", ", lost) + ")" : ""));
        // Every key the test buttons show is one the sources name, so none is left unread.
        string tests = File.ReadAllText(IoPath.Combine(folder, @"..\Scripts\Civic\CivicDirector.Construction.cs"));
        string options = File.ReadAllText(IoPath.Combine(folder, @"..\Scripts\Core\RealLifeOverhaulMod.cs"));
        bool named = true;
        foreach (string key in ConstructionKeys)
        {
            named &= key.StartsWith("reallife_contract_", StringComparison.Ordinal) || key == "reallife_construction_plot_place" || tests.Contains("\"" + key + "\"") || options.Contains("\"" + key + "\"");
        }
        Check(named, "construction: every test text is shown by a test button or its notices");
    }
}
