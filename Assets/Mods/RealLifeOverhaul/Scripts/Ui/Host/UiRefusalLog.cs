namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// A refused press written to Player.log, for every piece of the kit that sends presses (the host of a screen, the HUD,
    /// the cards over the world's items, the world prompt, the building's dock, the phone): the screen, the control with
    /// its words and why (UiRefusals), once for a control and a reason, with or without Developer diagnostics. The error
    /// sound alone told nobody which button had refused or why. Cost: nothing until a press is refused.
    /// </summary>
    internal static class UiRefusalLog
    {
        /// <summary>A target of a scene took no press: drawn disabled, or its screen answered false (the reason UiRefusals holds, if any was given).</summary>
        internal static void Say(string screen, UiScene scene, int node, bool disabled)
        {
            if (scene == null || node < 0 || node >= scene.NodeCount)
            {
                return;
            }
            string line = UiRefusals.Line(screen, scene.Nodes[node].Command, scene.Nodes[node].Arg, UiRefusals.Label(scene, node), disabled ? UiRefusals.Disabled : UiRefusals.Why);
            if (line != null)
            {
                UiDirector.Logger?.Warn(line);
            }
        }

        /// <summary>A press with no scene target behind it (a phone tile, an app's button) was refused.</summary>
        internal static void Say(string screen, int command, int arg, string label)
        {
            string line = UiRefusals.Line(screen, command, arg, label, UiRefusals.Why);
            if (line != null)
            {
                UiDirector.Logger?.Warn(line);
            }
        }
    }
}
