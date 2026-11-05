using System;
using System.Collections.Generic;
using RealLifeOverhaul.Ui;

// Brands, Ventures and the Web shop on the kit: every state laid out at every frame in every language (UiCases).
internal static partial class Program
{
    static partial void UiCasesVentures(UiScreenDef def, List<UiScreenCase> cases)
    {
        if (def.Id == "brands")
        {
            foreach (string state in UiSamples.BrandsStates)
            {
                string st = state;
                cases.Add(new UiScreenCase
                {
                    Def = def, Name = "brands (" + st + ")", Title = "Brands", Commands = VentFace.Commands,
                    Lay = (s, f, dark, k, t) => VentFace.Lay(s, UiSamples.Brands(t, st), f, dark, k)
                });
            }
        }
        if (def.Id == "ventures")
        {
            foreach (string state in UiSamples.VenturesStates)
            {
                string st = state;
                cases.Add(new UiScreenCase
                {
                    Def = def, Name = "ventures (" + st + ")", Title = "Ventures", Commands = VentFace.Commands,
                    Lay = (s, f, dark, k, t) => VentFace.Lay(s, UiSamples.Ventures(t, st), f, dark, k)
                });
            }
        }
    }
}
