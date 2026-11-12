using System.Collections.Generic;
using RealLifeOverhaul.Ui;

// The Parties app on the kit: every state laid out at every frame in every language (UiCases).
internal static partial class Program
{
    static partial void UiCasesParties(UiScreenDef def, List<UiScreenCase> cases)
    {
        if (def.Id != "parties")
        {
            return;
        }
        foreach (string state in UiSamples.PartiesStates)
        {
            string st = state;
            cases.Add(new UiScreenCase
            {
                Def = def, Name = "parties (" + st + ")", Title = "Parties", Commands = VentFace.Commands,
                Lay = (s, f, dark, k, t) => VentFace.Lay(s, UiSamples.Parties(t, st), f, dark, k)
            });
        }
    }
}
