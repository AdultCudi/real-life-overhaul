using System;
using System.Collections.Generic;
using Localizor;
using UI.Notification;

namespace RealLifeOverhaul.Meta
{
    /// <summary>What the options page asks and tells through the game's own confirm dialog and notifications (the dialog where there is no HUD). On a press only.</summary>
    internal static class OptionsNotice
    {
        private const string TextKey = "reallife_opt_text";   // "{text}": a text put together in code, shown as it is
        private const float Seconds = 12f;                    // long enough to read a sentence with a file path in it

        /// <summary>A question with a button that does it and one that does not. <paramref name="negative"/> gives the game's red look, for what cannot be undone.</summary>
        public static void Ask(string titleKey, string body, string yesKey, bool negative, Action yes)
        {
            try
            {
                GameConfirm.Show(titleKey.Localize(), TextKey.Localize(new { text = body }), () =>
                {
                    try
                    {
                        yes?.Invoke();
                    }
                    catch (Exception e)
                    {
                        MetaLog.Error(e);
                    }
                }, null, yesKey, null, false, negative);
            }
            catch (Exception e)
            {
                MetaLog.Error(e);
                MetaLog.Warn("could not ask: " + titleKey.GetLocalization() + ": " + body);
            }
        }

        /// <summary>Says what happened: the first paragraph as a notification, all of it in Player.log.</summary>
        public static void Tell(string titleKey, string text)
        {
            try
            {
                string title = titleKey.GetLocalization();
                MetaLog.Info(title + ": " + text.Replace("\n\n", " ").Replace('\n', ' '));
                if (Notifications.onShow != null)
                {
                    int end = text.IndexOf("\n\n", StringComparison.Ordinal);
                    var data = new Dictionary<string, string>(1) { { "text", end > 0 ? text.Substring(0, end) : text } };
                    Notifications.Show(NotificationType.Info, TextKey, data, Seconds, null, null, true, false);
                    return;
                }
                // No HUD: the dialog that asked is closing in this frame, so the answer is shown on the next one.
                MetaUi.Instance.NextFrame(() =>
                {
                    try
                    {
                        GameConfirm.Show(titleKey.Localize(), TextKey.Localize(new { text }), null, null, "reallife_opt_ok", null, false, false);
                    }
                    catch (Exception e)
                    {
                        MetaLog.Error(e);
                    }
                });
            }
            catch (Exception e)
            {
                MetaLog.Error(e);
            }
        }
    }
}
