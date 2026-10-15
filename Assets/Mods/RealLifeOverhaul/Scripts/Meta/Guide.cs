using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Dialogs;
using Entities;
using Extensions;
using Localizor;
using TMPro;
using UI.Smartphone.Apps.Contacts;
using UnityEngine;
using UnityEngine.UI;

namespace RealLifeOverhaul.Meta
{
    /// <summary>One page of the guide.</summary>
    public sealed class GuideTopic
    {
        public readonly string Id;
        public readonly string TitleKey;
        public readonly Func<string> Body;      // the text as written: "# " a heading, "- " a bullet, {name} a fact
        public string Title;

        public GuideTopic(string id, string titleKey, Func<string> body)
        {
            Id = id;
            TitleKey = titleKey;
            Body = body;
        }
    }

    /// <summary>
    /// The in-game guide: the pages and their texts. The app on the game's phone that shows them is <see cref="GuideApp"/>. The
    /// fixed pages are texts in en.json; a feature's page is its registered description and help text followed
    /// by its options as they are set now and its test buttons, both read from the registry. Numbers in a
    /// page are {facts} read from the code's own constants when the page is opened, and keys are read from
    /// <see cref="Keys"/>, so neither goes stale. Nothing here runs unless the guide is open.
    /// </summary>
    public static class Guide
    {
        public static void Open()
        {
            Open(null);
        }

        public static void Open(string topicId)
        {
            GuideApp.Open(topicId);
        }

        /// <summary>Opens the guide a moment from now: for the phone contact, whose call closes first.</summary>
        public static void OpenSoon()
        {
            MetaUi.Instance.Run(Soon());
        }

        private static IEnumerator Soon()
        {
            float until = Time.unscaledTime + 0.6f;
            while (Time.unscaledTime < until)
            {
                yield return null;
            }
            Open(null);
        }

        // ---------------------------------------------------------------- the topics

        public static List<GuideTopic> Topics()
        {
            var topics = new List<GuideTopic>
            {
                new GuideTopic("start", "reallife_guide_start_title", () => "reallife_guide_start".GetLocalization()),
                new GuideTopic("presets", "reallife_guide_presets_title", () => "reallife_guide_presets".GetLocalization() + "\n" + Presets.Table())
            };
            foreach (Feature feature in OptionsLayout.Ordered())
            {
                if (feature.HelpKey == null && feature.AboutKey == null)
                {
                    continue;
                }
                Feature captured = feature;
                topics.Add(new GuideTopic("feature_" + feature.Id, feature.TitleKey, () => FeaturePage(captured)));
            }
            topics.Add(new GuideTopic("trouble", "reallife_guide_trouble_title", () => "reallife_guide_trouble".GetLocalization() + "\n\n" + "reallife_guide_trouble_report".GetLocalization()   // "Report a bug" (Meta/BugReport)
                                                                                       + "\n\n" + "reallife_guide_trouble_update".GetLocalization()   // (Core/Guard)
                                                                                       + "\n\n" + "reallife_guide_trouble_stutter".GetLocalization()
                                                                                       + "\n\n" + "reallife_guide_trouble_empire".GetLocalization()));   // another author's dealership mod (Core/EmpireMotors)
            topics.Add(new GuideTopic("limits", "reallife_guide_limits_title", () => "reallife_guide_limits".GetLocalization()));
            topics.Add(new GuideTopic("credits", "reallife_guide_credits_title", () => "reallife_guide_credits".GetLocalization()));
            topics.Add(new GuideTopic("whatsnew", "reallife_guide_whatsnew_title", () => "reallife_guide_whatsnew".GetLocalization() + "\n\n" + Changelog.Page()));
            foreach (GuideTopic topic in topics)
            {
                topic.Title = topic.TitleKey.GetLocalization();
            }
            return topics;
        }

        private static string FeaturePage(Feature feature)
        {
            var text = new StringBuilder();
            if (feature.AboutKey != null)
            {
                text.Append(feature.AboutKey.GetLocalization()).Append("\n\n");
            }
            if (feature.HelpKey != null)
            {
                text.Append(feature.HelpKey.GetLocalization()).Append("\n\n");
            }

            bool any = false;
            foreach (OptionInfo info in SettingsTable.All)
            {
                if (info.Owner != feature || info.LabelKey == null)
                {
                    continue;
                }
                if (!any)
                {
                    any = true;
                    text.Append("# ").Append("reallife_guide_options".GetLocalization()).Append('\n');
                }
                text.Append("- ").Append(Presets.Label(info)).Append(": <b>").Append(Presets.Show(info, SettingsTable.Current(info))).Append("</b>");
                if (Features.NeedsReload(info.Id))
                {
                    text.Append("  ").Append("reallife_guide_needs_reload".GetLocalization());
                }
                text.Append('\n');
            }

            any = false;
            foreach (TestAction test in feature.Tests)
            {
                if (test.Run == null)
                {
                    continue;
                }
                if (!any)
                {
                    any = true;
                    text.Append("\n# ").Append("reallife_guide_tests".GetLocalization()).Append('\n');
                }
                text.Append("- ").Append(test.LabelKey.GetLocalization()).Append('\n');
            }
            return text.ToString();
        }

        /// <summary>
        /// The last line of an app's page: the Guide's page that explains it, named as the Guide's list names it
        /// ("Guide: <b>Stock market</b>"), at the page's foot in the box's note style (InfoBox.GuideLink). Every app ends its pages this way, so that what a figure means and how a
        /// rule works is read in the Guide and the page itself holds only the figures and the actions. A few strings,
        /// written with the page (on a click).
        /// </summary>
        public static void Point(GameUi.InfoBox box, string featureTitleKey)
        {
            if (box != null)
            {
                box.GuideLink(featureTitleKey.GetLocalization());
            }
        }

        // ---------------------------------------------------------------- facts and mark-up

        /// <summary>An amount of money as the game writes it.</summary>
        public static string Money(float amount)
        {
            try
            {
                return amount.ToCurrencyFormat();
            }
            catch (Exception)
            {
                return "$" + amount.ToString("0.##", CultureInfo.InvariantCulture);
            }
        }

        public static string Number(float value)
        {
            return value.ToString("0.##", CultureInfo.InvariantCulture);
        }

        public static string Percent(float share)
        {
            return (share * 100f).ToString("0.#", CultureInfo.InvariantCulture) + "%";
        }

        /// <summary>The value of a {name}, or null if nothing is called that.</summary>
        private static string Fact(string name)
        {
            switch (name)
            {
                case "version": return Changelog.Version;
                case "preset": return Presets.CurrentName();
                case "folder": return Presets.Folder;
                case "data": return Application.persistentDataPath;
                case "controls": return Controls();
            }
            for (int i = 0; i < Keys.Count; i++)
            {
                if (name == Keys.PrefsId((KeyAction)i))
                {
                    return Keys.Label((KeyAction)i);
                }
            }
            foreach (Feature feature in Features.All)
            {
                if (feature.Facts.TryGetValue(name, out Func<string> value))
                {
                    try
                    {
                        return value();
                    }
                    catch (Exception e)
                    {
                        MetaLog.Error(e);
                        return "?";
                    }
                }
            }
            return null;
        }

        // Every action with the key it is on now.
        private static string Controls()
        {
            var text = new StringBuilder();
            for (int i = 0; i < Keys.Count; i++)
            {
                var action = (KeyAction)i;
                text.Append("- ").Append(Keys.LabelKey(action).GetLocalization()).Append(": <b>").Append(Keys.Label(action)).Append("</b>\n");
            }
            return text.ToString();
        }

        /// <summary>Puts the facts into a text. A {name} nobody knows is left as it is (the set-up check reports it).</summary>
        public static string Fill(string text)
        {
            if (text == null || text.IndexOf('{') < 0)
            {
                return text;
            }
            var result = new StringBuilder(text.Length + 256);
            int at = 0;
            while (at < text.Length)
            {
                int open = text.IndexOf('{', at);
                int close = open < 0 ? -1 : text.IndexOf('}', open);
                if (open < 0 || close < 0)
                {
                    result.Append(text, at, text.Length - at);
                    break;
                }
                result.Append(text, at, open - at);
                string value = Fact(text.Substring(open + 1, close - open - 1));
                if (value != null)
                {
                    result.Append(value);
                }
                else
                {
                    result.Append(text, open, close - open + 1);
                }
                at = close + 1;
            }
            return result.ToString();
        }

        /// <summary>A page as TextMeshPro draws it: facts filled in, headings and bullets marked up.</summary>
        public static string Render(GuideTopic topic)
        {
            string filled = Fill(topic.Body()) ?? string.Empty;
            var text = new StringBuilder(filled.Length + 512);
            foreach (string raw in filled.Replace("\r", string.Empty).Split('\n'))
            {
                if (raw.StartsWith("# ", StringComparison.Ordinal))
                {
                    text.Append("\n<size=118%><b><color=#FF9414>").Append(raw, 2, raw.Length - 2).Append("</color></b></size>\n");
                }
                else if (raw.StartsWith("- ", StringComparison.Ordinal))
                {
                    text.Append("    -  ").Append(raw, 2, raw.Length - 2).Append('\n');
                }
                else
                {
                    text.Append(raw).Append('\n');
                }
            }
            return text.ToString();
        }

        /// <summary>For the set-up check: every {name} in every page that nothing fills, and every text that is missing.</summary>
        public static void Check(List<string> problems)
        {
            foreach (GuideTopic topic in Topics())
            {
                if (topic.Title == topic.TitleKey)
                {
                    problems.Add("guide: the title " + topic.TitleKey + " is not in the locale file");
                }
                string body;
                try
                {
                    body = Fill(topic.Body());
                }
                catch (Exception e)
                {
                    problems.Add("guide: the page " + topic.Id + " failed: " + e.Message);
                    continue;
                }
                int at = 0;
                while (body != null && (at = body.IndexOf('{', at)) >= 0)
                {
                    int close = body.IndexOf('}', at);
                    if (close < 0)
                    {
                        break;
                    }
                    problems.Add("guide: the page " + topic.Id + " has nothing for " + body.Substring(at, close - at + 1));
                    at = close + 1;
                }
                // A text that is missing comes back as its own key: every key of the mod's starts the same way.
                int found = body != null ? body.IndexOf("reallife", StringComparison.Ordinal) : -1;
                while (found >= 0)
                {
                    if (found + 8 < body.Length && body[found + 8] == '_')
                    {
                        problems.Add("guide: the page " + topic.Id + " shows a raw key near \"" + body.Substring(found, Math.Min(40, body.Length - found)) + "\"");
                        break;
                    }
                    found = body.IndexOf("reallife", found + 8, StringComparison.Ordinal);
                }
            }
        }
    }

    /// <summary>
    /// The "Real Life Overhaul Guide" entry on the phone: a contact like the mod's others, whose call opens the
    /// guide. Added with the other contacts (and only while "phone contacts" is on); removed with them by the save tools.
    /// </summary>
    public static class GuideContact
    {
        public const string ContactId = "reallife-guide";

        /// <summary>Returns true if the contact is in the save after the call.</summary>
        public static bool Setup()
        {
            if (!Settings.PhoneContacts || SaveGameManager.Current == null)
            {
                return false;
            }
            try
            {
                ContactPictures.Add(ContactId);   // the game's own logo shapes or portrait camera (Phone/Contacts)
                Contact contact = Contact.GetContact(ContactId, ContactCategoryName.General, "reallife_contact_guide_desc", null, false, true);
                var dialogType = (CallDialogType)BAModAPI.ModEnumHash.GetSafeHash("reallife_guide_calldialogtype");
                contact.callDialogTypeOverride = dialogType;   // not saved by the game, so set on every load
                CallDialogFactory.RegisterDialog(dialogType, Guard.Maker(() => new GuideDialog()));
                if (contact.messagesQueue == null || contact.messagesQueue.Count == 0)
                {
                    contact.SendMessage(new TextMessage("reallife_text_guide_welcome"), notify: false);
                }
                return true;
            }
            catch (Exception e)
            {
                MetaLog.Error(e);
                MetaLog.Warn("the guide's phone contact could not be added; the guide is still on the options page.");
                return false;
            }
        }

    }

    /// <summary>The call to the guide contact: one line, then the guide opens.</summary>
    public class GuideDialog : Dialog
    {
        public GuideDialog()
        {
            npcNameKey = "reallife-guide";
            DialogController.current.ShowEntry(Guard.Entry(PhoneContacts.Closing("reallife_guide_call")));
            Guide.OpenSoon();
        }
    }
}
