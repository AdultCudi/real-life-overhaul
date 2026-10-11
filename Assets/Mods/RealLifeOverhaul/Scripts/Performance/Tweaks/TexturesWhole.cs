using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The game streams its textures: every frame the processor works out, for a share of the renderers, how much
    /// detail each texture needs, and loads or drops levels of it to stay inside 1.5 GB of the graphics card's
    /// memory (measured in perf_140253: 0.33 ms of the main thread's frame, EarlyUpdate.UpdateTextureStreamingManager).
    /// A card with plenty of memory has no need of that: with this on, every texture is kept whole on the card and
    /// the processor's part is not run. It trades the card's memory for the processor's time, so it is only done on
    /// a card with at least MostlyFree megabytes, and it is off unless the player switches it on: the first frames
    /// after it comes on load the missing detail, which can be felt once.
    /// Nothing is changed but the one engine setting, which is put back as it was found.
    /// </summary>
    public sealed class TexturesWhole : PerfTweak
    {
        private const int MostlyFree = 10000;   // megabytes of graphics memory from which whole textures are safe (the game's own budget is 1,536)

        private bool _was;
        private bool _held;

        public override string Id => "textures_whole";
        public override string LabelKey => "reallife_perf_textures_whole";
        public override bool DefaultOn => false;

        protected override void Apply()
        {
            _held = false;
            if (SystemInfo.graphicsMemorySize < MostlyFree)
            {
                return;   // (said in Describe; the game's streaming stays as it is)
            }
            _was = QualitySettings.streamingMipmapsActive;
            QualitySettings.streamingMipmapsActive = false;
            _held = true;
        }

        protected override void Restore()
        {
            if (_held)
            {
                QualitySettings.streamingMipmapsActive = _was;
            }
            _held = false;
        }

        public override string Describe()
        {
            return _held
                ? "textures kept whole on the graphics card (" + SystemInfo.graphicsMemorySize + " MB); the game had streaming " + (_was ? "on" : "off")
                : "not done: the graphics card has " + SystemInfo.graphicsMemorySize + " MB, under the " + MostlyFree + " MB this needs";
        }
    }
}
