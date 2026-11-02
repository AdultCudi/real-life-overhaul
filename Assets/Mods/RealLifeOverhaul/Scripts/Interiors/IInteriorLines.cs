using System;
using RealLifeOverhaul.GameUi;

namespace RealLifeOverhaul.Interiors
{
    /// <summary>
    /// What the Interior tab writes, a line at a time: into the game's own boxes in BizMan's own view, or into the RLO
    /// interface's model when BizMan is drawn on it. The tab's pages write the same lines either way.
    /// </summary>
    public interface IInteriorLines
    {
        void Title(string words);
        void Headline(string key);
        void HeadlineWords(string words);
        void Line(string words);
        void Line(string words, CellTone tone);
        void Note(string words);
        void Button(int kind, string key, Action click);   // kind: InfoBox.Positive, Negative or Additional
        void Guide(string titleKey);                       // the Guide's page that explains the tab
        void Settings(string sectionId);                   // the mod's settings at the tab's section
    }

    /// <summary>The game's own box as the tab's lines: each line written into it as the tab wrote it before.</summary>
    internal sealed class BoxLines : IInteriorLines
    {
        private readonly InfoBox _box;

        public BoxLines(InfoBox box)
        {
            _box = box;
        }

        public void Title(string words) => _box.Title(words);

        public void Headline(string key) => _box.Headline(key);

        public void HeadlineWords(string words) => _box.HeadlineWords(words);

        public void Line(string words) => _box.Line(words);

        public void Line(string words, CellTone tone) => _box.Line(words, tone);

        public void Note(string words) => _box.Note(words);

        public void Button(int kind, string key, Action click) => InfoBox.Button(_box.ButtonRow(), kind, key, click);

        public void Guide(string titleKey) => Meta.Guide.Point(_box, titleKey);

        public void Settings(string sectionId) => Meta.SettingsApp.Link(_box, sectionId);
    }
}
