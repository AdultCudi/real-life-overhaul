using RealLifeOverhaul.Photos;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The Camera app on the kit. Opening it still opens the camera. This page is what remains when the camera is refused:
    /// the reason, how full the gallery is, and the way into the Gallery. Cost: a layout when it opens.
    /// </summary>
    public sealed class CameraScreen : IUiScreen
    {
        private readonly PhotosDirector _director;
        private readonly UiDecisionModel _model = new UiDecisionModel();
        private bool _dirty = true;

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

        public bool Press(int command, int arg)
        {
            if (command == UiDecision.ChoiceCommand && arg == 0)
            {
                _director?.OpenGallery();
                return true;
            }
            return false;
        }

        public void Focused(int command, int arg)
        {
        }

        public bool Back() => false;

        public void Tab(int step)
        {
        }

        public bool Act(UiAction action)
        {
            if (action == UiAction.Alt)
            {
                _director?.OpenGallery();
                return true;
            }
            return false;
        }

        public void Reveal(int list, int index)
        {
        }

        public void Wheel(int clip, float notches)
        {
        }

        private void Fill()
        {
            int count = _director != null ? _director.Count : 0;
            CameraRefusal why = _director != null ? CameraRules.Refuse(PhotosGame.Situation(false)) : CameraRefusal.NotLoaded;
            string key = CameraRules.RefusalKey(why);
            PhotoEntry newest = _director != null && count > 0 ? _director.Photos[0] : null;
            _model.Kicker = newest != null
                ? UiText.Fill("reallife_ui_life_newest", "taken", PhotosText.Taken(newest))
                : UiText.T("reallife_ui_life_empty_gallery_title");
            _model.Title = count >= PhotoStoreRules.Cap ? UiText.T("reallife_ui_life_camera_full_title") : UiText.T("reallife_phonecam_app");
            _model.Body = count >= PhotoStoreRules.Cap
                ? UiText.Fill("reallife_ui_life_camera_full_body", "cap", PhotoStoreRules.Cap.ToString(), "count", count.ToString())
                : key != null ? UiText.T(key) : UiText.T("reallife_ui_life_camera_full_body");
            _model.Emblem = UiIcon.Camera;
            _model.EmblemTint = UiRole.Accent;
            _model.EmblemLabel = null;
            _model.Choices = new[]
            {
                new UiDecisionChoice
                {
                    Key = UiAction.Alt, Name = UiText.T("reallife_ui_life_open_gallery"),
                    Lines = new[] { new UiConsequence { Text = UiText.T("reallife_ui_life_camera_stay"), Tone = 0, Change = 0 } },
                },
            };
            _model.Selected = 0;
            _model.Buttons = new[]
            {
                new UiActionModel { Label = UiText.T("common_close"), Kind = UiButtonKind.Secondary, Key = UiAction.Back },
            };
            _model.Prompts = new[]
            {
                new UiPrompt { Action = UiAction.Move, Label = UiText.T("reallife_ui_prompt_move") },
                new UiPrompt { Action = UiAction.Confirm, Label = UiText.T("reallife_ui_prompt_select") },
                new UiPrompt { Action = UiAction.Back, Label = UiText.T("reallife_ui_prompt_back") },
            };
            _model.PromptCount = 3;
        }
    }
}
