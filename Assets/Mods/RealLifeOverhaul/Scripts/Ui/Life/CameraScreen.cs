using RealLifeOverhaul.Photos;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The Camera app on the kit. Opening it still opens the camera. This page is what remains when the camera is refused:
    /// the reason, how full the gallery is, and the way into the Gallery; its Close button, its close key and Back close the
    /// app, as the other apps' pages close. Its words and answers are CameraPage's. Cost: a layout when it opens.
    /// </summary>
    public sealed class CameraScreen : IUiScreen
    {
        private readonly PhotosDirector _director;
        private readonly UiDecisionModel _model = new UiDecisionModel();
        private bool _dirty = true;

        /// <summary>Closes the Camera app: the full menu put away. Set by the app, which knows the menu.</summary>
        public System.Action Close;

        public CameraScreen(PhotosDirector director)
        {
            _director = director;
        }

        public string Id => "camera";

        public bool Dirty => _dirty;

        public void Lay(UiScene s, UiFrame frame, bool dark, IUiKeys keys)
        {
            Fill();
            _dirty = false;
            LifePages.Refused(s, _model, frame, dark, keys);
        }

        public bool Press(int command, int arg) => Do(CameraPage.Answer(command, arg));

        public void Focused(int command, int arg)
        {
        }

        public bool Back() => Do(CameraAnswer.Close);

        public void Tab(int step)
        {
        }

        public bool Act(UiAction action) => Do(CameraPage.Key(action));

        public void Reveal(int list, int index)
        {
        }

        public void Wheel(int clip, float notches)
        {
        }

        private bool Do(CameraAnswer answer)
        {
            switch (answer)
            {
                case CameraAnswer.Gallery:
                    _director?.OpenGallery();
                    return true;
                case CameraAnswer.Close:
                    Close?.Invoke();
                    return Close != null;
                default:
                    return false;
            }
        }

        private void Fill()
        {
            int count = _director != null ? _director.Count : 0;
            CameraRefusal why = _director != null ? CameraRules.Refuse(PhotosGame.Situation(false)) : CameraRefusal.NotLoaded;
            PhotoEntry newest = _director != null && count > 0 ? _director.Photos[0] : null;
            CameraPage.Fill(_model, count, CameraRules.RefusalKey(why), newest != null ? PhotosText.Taken(newest) : null, UiText.T);
        }
    }
}
