using System.Collections.Generic;
using RealLifeOverhaul.Ui;

public static partial class UiPreviewRunner
{
    // Property and the Car Showroom: every state of both apps over the street, the rents map over the city's own rent picture.
    static partial void AddEstate(List<Screen> screens)
    {
        for (int i = 0; i < UiSamples.EstateStates.Length; i++)
        {
            string st = UiSamples.EstateStates[i];
            bool rents = st.StartsWith("rents", System.StringComparison.Ordinal);
            screens.Add(new Screen
            {
                Name = "estate_" + st,
                AppName = "Property",
                Icon = UiIcon.Home,
                Backdrop = rents ? "map_rent.png" : st.StartsWith("unit", System.StringComparison.Ordinal) || st == "building" ? "12.jpg" : i % 2 == 0 ? "13.jpg" : "02.jpg",
                HoverCommand = UiAppWindow.NavCommand,
                HoverArg = 1,
                Walk = Moves("DDR"),
                Lay = (s, f, d, k) => EstateFace.Lay(s, UiSamples.Estate(English, st), f, d, k)
            });
        }
        for (int i = 0; i < UiSamples.DealerStates.Length; i++)
        {
            string st = UiSamples.DealerStates[i];
            screens.Add(new Screen
            {
                Name = "dealer_" + st,
                AppName = "Car Showroom",
                Icon = UiIcon.Car,
                Backdrop = st.StartsWith("auction", System.StringComparison.Ordinal) ? "02.jpg" : i % 2 == 0 ? "13.jpg" : "08.jpg",
                HoverCommand = UiAppWindow.NavCommand,
                HoverArg = 1,
                Walk = Moves("DDR"),
                Lay = (s, f, d, k) => EstateFace.Lay(s, UiSamples.Dealer(English, st), f, d, k)
            });
        }
    }
}
