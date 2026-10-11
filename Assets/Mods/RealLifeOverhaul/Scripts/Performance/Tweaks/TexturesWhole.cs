using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The game streams its textures: every frame the processor works out, for a share of the renderers, how much
    /// detail each texture needs, and loads or drops levels of it to stay inside 1.5 GB of the graphics card's
    /// memory (measured in perf_140253: 0.33 ms of the main thread's frame, EarlyUpdate.UpdateTextureStreamingManager).
    /// A card with plenty of memory has no need of that: with this on, every texture is kept whole on the card and
    /// the processor's part is not run. It trades the card's memory for the processor's time, so it is only done
    /// where everything loaded, kept whole, takes less than half the card's memory, and it is off unless the player
    /// switches it on: the first frames after it comes on load the missing detail, which can be felt once.
    /// The setting belongs to the game's graphics quality level. When the player picks another level the engine
    /// brings that level's own setting in, so every few seconds this looks whether the level is still the one it
    /// changed, and if not, takes the new level's setting as what to put back and switches streaming off again.
    /// Nothing is changed but that one setting, and it is put back as found on the level it was found on.
    /// (A level left behind while this was on keeps streaming off until the game is started again: the engine only
    /// lets the level in use be changed.)
    /// </summary>
    public sealed class TexturesWhole : PerfTweak
    {
        private const float Share = 0.5f;   // of the card's memory that all textures, whole, may take

        private bool _was;
        private bool _held;
        private int _level = -1;
        private string _why = string.Empty;

        public override string Id => "textures_whole";
        public override string LabelKey => "reallife_perf_textures_whole";
        public override bool DefaultOn => false;
        public override float Interval => 3f;

        protected override void Apply()
        {
            _held = false;
            Take();
        }

        protected override void Tick(float now)
        {
            if (!_held)
            {
                Take();   // (more may be loaded, or less, than when it was last weighed)
            }
            else if (QualitySettings.GetQualityLevel() != _level || QualitySettings.streamingMipmapsActive)
            {
                // The player picked another quality level (or the game put the setting back): that level's own setting is what is owed back now.
                _level = QualitySettings.GetQualityLevel();
                _was = QualitySettings.streamingMipmapsActive;
                QualitySettings.streamingMipmapsActive = false;
            }
        }

        protected override void Restore()
        {
            if (_held && QualitySettings.GetQualityLevel() == _level)
            {
                QualitySettings.streamingMipmapsActive = _was;
            }
            _held = false;
            _level = -1;
        }

        private void Take()
        {
            long card = (long)SystemInfo.graphicsMemorySize * 1024L * 1024L;
            long whole = (long)Texture.totalTextureMemory;
            if (card <= 0L || whole > card * Share)
            {
                _why = "all textures kept whole would take " + (whole >> 20) + " MB of the card's " + SystemInfo.graphicsMemorySize + " MB, over the half this allows";
                return;
            }
            _level = QualitySettings.GetQualityLevel();
            _was = QualitySettings.streamingMipmapsActive;
            QualitySettings.streamingMipmapsActive = false;
            _held = true;
        }

        public override string Describe()
        {
            return _held
                ? "textures kept whole on the graphics card (" + ((long)Texture.totalTextureMemory >> 20) + " MB of " + SystemInfo.graphicsMemorySize
                  + " MB); quality level " + _level + " had streaming " + (_was ? "on" : "off")
                : "not done: " + _why;
        }
    }
}
