namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The frames a second for the HUD's status bar. The kit's HUD hides the game's own HUD, and the game's FpsMeter label
    /// (a label on the HUD) goes with it, so the bar shows the figure itself while the game's show-FPS option is on
    /// (PlayerPrefSettings.showFps, the option FpsMeter reads). The frames are counted on the unscaled clock and the figure
    /// is worked out twice a second, as the game's own meter does. Cost a frame: one comparison while the option is off (the
    /// option itself is read once a second); a counter and a comparison while it is on. Nothing is allocated.
    /// </summary>
    public static class HudFps
    {
        /// <summary>The frames a second, or 0 while the option is off or no reading is in yet.</summary>
        public static int Value { get; private set; }

        private const float OptionEvery = 1f;   // s: how often the option is read
        private const float Interval = 0.5f;    // s: the reading's length, as the game's meter takes it

        private static bool _on;
        private static float _optionAt = -1f;   // the unscaled time the option is read next
        private static float _since;
        private static int _frames;

        /// <summary>Counts one frame; called once a frame by the HUD host with the unscaled time.</summary>
        public static void Tick(float now)
        {
            if (now >= _optionAt)
            {
                _optionAt = now + OptionEvery;
                bool on = PlayerPrefSettings.showFps;
                if (on != _on)
                {
                    _on = on;
                    _frames = 0;
                    _since = now;
                    Value = 0;
                }
            }
            if (!_on)
            {
                return;
            }
            _frames++;
            float span = now - _since;
            if (span >= Interval)
            {
                Value = UiHudRules.Fps(_frames, span);
                _frames = 0;
                _since = now;
            }
        }
    }
}
