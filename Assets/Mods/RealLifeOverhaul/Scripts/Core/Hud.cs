using TMPro;

namespace RealLifeOverhaul
{
    /// <summary>
    /// What is left of the mod's old on-screen layer (a canvas with a crosshair, a marker and a speed readout): nothing is drawn any more.
    /// The crosshair is <see cref="CrosshairDot"/>, markers are <see cref="GameUi.GuideMark"/>, the speed is <see cref="SpeedReading"/>
    /// in the game's vehicle panel. Only the font is kept under its old name, so that the bus stops' lettering (Scripts/Transport, which
    /// was not touched in this piece of work) goes on compiling; it reads <see cref="GameUi.GameFont"/>. Delete this file when
    /// Transport reads that instead.
    /// </summary>
    public static class Hud
    {
        public static TMP_FontAsset Font => GameUi.GameFont.Asset;
    }
}
