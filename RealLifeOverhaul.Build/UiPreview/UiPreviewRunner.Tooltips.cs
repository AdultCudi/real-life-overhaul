using System.Collections.Generic;
using RealLifeOverhaul.Ui;

public static partial class UiPreviewRunner
{
    // The tooltip card over a street: an item, an employee and Build mode's hover tip where the pointer rests (the card hangs above it, as the
    // game's does), and the net worth card over the money pill; by day and at night. One shot each: the card takes no target and no key.
    private static void AddTooltips(List<Screen> screens)
    {
        string[] states = { "item", "employee", "worth", "build", "worth_early" };
        string[] backdrops = { "13.jpg", "13.jpg", "11.jpg", "11.jpg", "13.jpg" };
        float[] x = { 0.42f, 0.42f, 0f, 0.55f, 0f }, y = { 0.62f, 0.66f, 0f, 0.5f, 0f };
        for (int i = 0; i < states.Length; i++)
        {
            string st = states[i];
            float px = x[i], py = y[i];
            screens.Add(new Screen
            {
                Name = "tooltip_" + st, AppName = "Tooltip", Icon = UiIcon.Info, Backdrop = backdrops[i], NoKeys = true, Single = true,
                Lay = (s, f, d, k) => UiSamples.TooltipScene(s, f, d, k, English, st, px, py)
            });
        }
    }
}
