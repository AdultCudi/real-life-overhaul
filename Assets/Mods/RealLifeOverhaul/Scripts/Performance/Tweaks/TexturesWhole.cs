using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// Texture streaming off (0.33 ms a frame), where every texture kept whole takes under half the card; off by default.
    /// Only the quality level in use is changed; another level picked is followed, one left keeps it until a restart.
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
                Take();   // more may be loaded, or less, than when last weighed
            }
            else if (QualitySettings.GetQualityLevel() != _level || QualitySettings.streamingMipmapsActive)
            {
                // another quality level picked, or the game put the setting back: that level's setting is owed back now
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
