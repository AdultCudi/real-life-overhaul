using System.Collections.Generic;
using RealLifeOverhaul.Ui;

public static partial class UiPreviewRunner
{
    // My Employees on the kit in every state, over the day street the full menu leaves (the training card indoors, where a
    // business keeps its staff), with the frame's app row under it; and a rival's staff over BizMan's street.
    static partial void AddEmployees(List<Screen> screens)
    {
        foreach (string state in UiSamples.StaffStates)
        {
            string st = state;
            bool person = st.StartsWith("person") || st == "training_card";
            var screen = new Screen
            {
                Name = "staff_" + st, AppName = "My Employees", Icon = UiIcon.People, Backdrop = st == "training_card" ? "05.jpg" : st.EndsWith("_fr") ? "11.jpg" : "13.jpg",
                Bar = () => UiSamples.ShellBar(4, false), Lay = (s, f, d, k) => UiStaff.Lay(s, UiSamples.Staff(English, st), f, d, k)
            };
            if (st == "training_card")
            {
                screen.HoverCommand = UiCard.ButtonCommand;
                screen.HoverArg = 0;
                screen.Walk = new[] { Down, Up };
            }
            else if (person)
            {
                screen.HoverCommand = UiAppWindow.ActionCommand;
                screen.HoverArg = 1;
                screen.FocusCommand = UiStaff.TrainCommand;
                screen.Walk = new[] { Down, Down, Down, Down, Down, Right, Up, Up, Left };
            }
            else if (st == "mass_ask" || st == "filter_open")
            {
                screen.HoverCommand = UiStaff.OptionCommand;
                screen.HoverArg = 2;
                screen.Walk = new[] { Down, Down, Up };
            }
            else if (st == "employees_0")
            {
                screen.HoverCommand = UiAppWindow.NavCommand;
                screen.HoverArg = 1;
                screen.Walk = new[] { Down, Right, Left };
            }
            else
            {
                screen.HoverCommand = UiDataTable.First + UiDataTable.Row;
                screen.HoverArg = 2;
                screen.FocusCommand = UiDataTable.First + UiDataTable.Row;
                screen.FocusArg = 1;
                screen.Walk = new[] { Down, Down, Down, Right, Right, Down, Down, Left };
            }
            screens.Add(screen);
        }
        screens.Add(new Screen
        {
            Name = "staff_rival", AppName = "BizMan", Icon = UiIcon.Flag, Backdrop = "02.jpg", Bar = () => UiSamples.ShellBar(UiSamples.RowBizMan, false),
            Lay = (s, f, d, k) => UiStaff.Lay(s, UiSamples.RivalStaff(English), f, d, k), HoverCommand = UiDataTable.First + UiDataTable.Row, HoverArg = 3,
            FocusCommand = UiDataTable.First + UiDataTable.Row, FocusArg = 2, Walk = new[] { Down, Down, Down, Up, Up, Up, Right }
        });
    }
}
