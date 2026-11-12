using System.Collections.Generic;
using RealLifeOverhaul.Ui;

internal static partial class Program
{
    private static readonly int[] MapCommands = MapPresses.Commands;

    // The city map's panels, every state each has: the filters (with a search, a search that finds nothing, another mod's group, the taxi's note), the place card in its tabs,
    // the subway's stations, the choice of a place, and the district view in its layers. Laid out in every language, frame and input as any screen.
    static partial void UiCasesMap(UiScreenDef def, List<UiScreenCase> cases)
    {
        switch (def.Id)
        {
            case "map_filters":
                foreach (string state in new[] { "filters", "search", "search_none", "foreign", "taxi" })
                {
                    string st = state;
                    cases.Add(new UiScreenCase { Def = def, Name = def.Id + " (" + st + ")", Title = "City map", Commands = MapCommands, Overlay = true,
                        Lay = (s, f, d, k, t) => UiSamples.MapFiltersScene(s, f, d, k, t, st) });
                }
                foreach (string state in UiSamples.MapPlaceStates)
                {
                    string st = state;
                    cases.Add(new UiScreenCase { Def = def, Name = def.Id + " (place " + st + ")", Title = "City map", Commands = MapCommands, Overlay = true,
                        Lay = (s, f, d, k, t) => UiSamples.MapPlaceScene(s, f, d, k, t, st) });
                }
                break;
            case "map_subway":
                cases.Add(new UiScreenCase { Def = def, Name = def.Id + " (subway)", Title = "City map", Commands = MapCommands, Overlay = true,
                    Lay = (s, f, d, k, t) => UiSamples.MapFiltersScene(s, f, d, k, t, "subway") });
                break;
            case "map_picker":
                foreach (string state in new[] { "picker", "picker_long" })
                {
                    string st = state;
                    cases.Add(new UiScreenCase { Def = def, Name = def.Id + " (" + st + ")", Title = "City map", Commands = MapCommands, Overlay = true,
                        Lay = (s, f, d, k, t) => UiSamples.MapFiltersScene(s, f, d, k, t, st) });
                }
                break;
            case "map_district":
                foreach (string state in UiSamples.MapDistrictStates)
                {
                    string st = state;
                    cases.Add(new UiScreenCase { Def = def, Name = def.Id + " (" + st + ")", Title = "City map", Commands = MapCommands, Overlay = true,
                        Lay = (s, f, d, k, t) => UiSamples.MapDistrictScene(s, f, d, k, t, st) });
                }
                break;
        }
    }
}
