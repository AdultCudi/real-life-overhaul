using System;
using RealLifeOverhaul.Photos;

namespace RealLifeOverhaul.Ui
{
    /// <summary>What a press on the Camera's page does.</summary>
    public enum CameraAnswer
    {
        None,
        Gallery,   // the way into the Gallery
        Close      // the Camera app is closed, back to the phone's way out: the game
    }

    /// <summary>
    /// The Camera's page on the kit, game-free: the words it shows for each state (the camera refused, the gallery full, the page
    /// left open with neither) and what each of its targets does, so EconomyCheck lays it out and presses every target. CameraScreen
    /// runs it. Cost: a few strings when the page is laid out.
    /// </summary>
    public static class CameraPage
    {
        /// <summary>The page's body: the full gallery's text with its numbers filled, the reason the camera is refused, or the page's note.</summary>
        public static string Body(int count, string refusalKey, Func<string, string> t)
        {
            if (count >= PhotoStoreRules.Cap)
            {
                return t("reallife_ui_life_camera_full_body")
                    .Replace("{cap}", PhotoStoreRules.Cap.ToString(System.Globalization.CultureInfo.InvariantCulture))
                    .Replace("{count}", count.ToString(System.Globalization.CultureInfo.InvariantCulture));
            }
            return t(refusalKey ?? "reallife_phonecam_page_note");
        }

        /// <summary>Fills the page: newestTaken is the newest photo's time in the game's words, or null with no photos.</summary>
        public static void Fill(UiDecisionModel m, int count, string refusalKey, string newestTaken, Func<string, string> t)
        {
            m.Kicker = newestTaken != null ? t("reallife_ui_life_newest").Replace("{taken}", newestTaken) : t("reallife_ui_life_empty_gallery_title");
            m.Title = count >= PhotoStoreRules.Cap ? t("reallife_ui_life_camera_full_title") : t("reallife_phonecam_app");
            m.Body = Body(count, refusalKey, t);
            m.Emblem = UiIcon.Camera;
            m.EmblemTint = UiRole.Accent;
            m.EmblemLabel = null;
            m.Choices = new[]
            {
                new UiDecisionChoice
                {
                    Key = UiAction.Alt, Name = t("reallife_ui_life_open_gallery"),
                    Lines = new[] { new UiConsequence { Text = t("reallife_ui_life_camera_stay"), Tone = 0, Change = 0 } },
                },
            };
            m.Selected = 0;
            m.Buttons = new[]
            {
                new UiActionModel { Label = t("common_close"), Kind = UiButtonKind.Secondary, Key = UiAction.Back },
            };
            m.Prompts = new[]
            {
                new UiPrompt { Action = UiAction.Move, Label = t("reallife_ui_prompt_move") },
                new UiPrompt { Action = UiAction.Confirm, Label = t("reallife_ui_prompt_select") },
                new UiPrompt { Action = UiAction.Back, Label = t("reallife_ui_prompt_back") },
            };
            m.PromptCount = 3;
        }

        /// <summary>What a press on a target of the page does: the choice opens the Gallery, the Close button closes the app.</summary>
        public static CameraAnswer Answer(int command, int arg)
        {
            if (command == UiDecision.ChoiceCommand && arg == 0)
            {
                return CameraAnswer.Gallery;
            }
            return command == UiDecision.ButtonCommand && arg == 0 ? CameraAnswer.Close : CameraAnswer.None;
        }

        /// <summary>What a key of the page does without a target: the choice's own key opens the Gallery.</summary>
        public static CameraAnswer Key(UiAction action) => action == UiAction.Alt ? CameraAnswer.Gallery : CameraAnswer.None;
    }
}
