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
    /// The in-game guide: a list of topics on the left, the page on the right, on the mod's own canvas. The
    /// fixed pages are texts in en.json; a feature's page is its registered description and help text followed
    /// by its options as they are set now and its test buttons, both read from the registry. Numbers in a
    /// page are {facts} read from the code's own constants when the page is opened, and keys are read from
    /// <see cref="Keys"/>, so neither goes stale. Nothing here runs unless the guide is open.
    /// </summary>
    public static class Guide
    {
        private static GuidePanel _panel;

        public static void Open()
        {
            Open(null);
        }

        public static void Open(string topicId)
        {
            try
            {
                MetaUi ui = MetaUi.Instance;
                if (_panel == null || _panel.Root == null)
                {
                    _panel = new GuidePanel(ui);
                }
                _panel.Prepare(topicId);
                ui.Show(_panel);
            }
            catch (Exception e)
            {
                MetaLog.Error(e);
                MetaLog.Warn("the guide could not be opened.");
            }
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
            topics.Add(new GuideTopic("trouble", "reallife_guide_trouble_title", () => "reallife_guide_trouble".GetLocalization() + "\n\n" + "reallife_guide_trouble_stutter".GetLocalization()));
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

        // ---------------------------------------------------------------- the panel

        private sealed class GuidePanel : MetaPanel
        {
            private sealed class Row
            {
                public GuideTopic Topic;
                public GameObject Object;
                public Image Plate;
                public string Searchable;
            }

            private readonly MetaUi _ui;
            private readonly List<Row> _rows = new List<Row>();
            private readonly RectTransform _list;
            private readonly TextMeshProUGUI _page;
            private readonly TextMeshProUGUI _search;
            private readonly StringBuilder _query = new StringBuilder(32);
            private Row _selected;

            public GuidePanel(MetaUi ui)
            {
                _ui = ui;
                Root = ui.Frame("Guide", new Vector2(1640f, 960f), out RectTransform window);
                RectTransform bar = ui.TitleBar(window, "reallife_guide_window_title".GetLocalization().Replace("{version}", Changelog.Version), out TextMeshProUGUI _);

                Button close = ui.Button("Close", bar, "reallife_meta_close".GetLocalization(), () => _ui.Close(this), 40f, 18f);
                var closeRect = (RectTransform)close.transform;
                closeRect.anchorMin = closeRect.anchorMax = closeRect.pivot = new Vector2(1f, 0.5f);
                closeRect.anchoredPosition = new Vector2(-12f, 0f);
                closeRect.sizeDelta = new Vector2(170f, 40f);

                _search = ui.Text("Search", bar, 19f, MetaUi.Soft, TextAlignmentOptions.MidlineRight, false);
                _search.rectTransform.anchorMin = new Vector2(0.35f, 0f);
                _search.rectTransform.anchorMax = new Vector2(1f, 1f);
                _search.rectTransform.offsetMin = Vector2.zero;
                _search.rectTransform.offsetMax = new Vector2(-200f, 0f);

                ScrollRect list = ui.Scroll("Topics", window, out _list, 6f);
                var listRect = (RectTransform)list.transform;
                listRect.anchorMin = new Vector2(0f, 0f);
                listRect.anchorMax = new Vector2(0f, 1f);
                listRect.pivot = new Vector2(0f, 0.5f);
                listRect.offsetMin = new Vector2(14f, 14f);
                listRect.offsetMax = new Vector2(414f, -70f);

                KeyScroll = ui.Scroll("Page", window, out RectTransform content, 0f);
                MetaUi.Stretch((RectTransform)KeyScroll.transform, 426f, 14f, 14f, 70f);
                _page = ui.Text("Text", content, 21f, MetaUi.Ink, TextAlignmentOptions.TopLeft, true);
                _page.lineSpacing = 6f;
            }

            /// <summary>Before each showing: the topic list is made again (cheap), so it follows the registry and the language.</summary>
            public void Prepare(string topicId)
            {
                string keep = topicId ?? (_selected != null ? _selected.Topic.Id : null);
                foreach (Row row in _rows)
                {
                    if (row.Object != null)
                    {
                        row.Object.SetActive(false);
                        UnityEngine.Object.Destroy(row.Object);
                    }
                }
                _rows.Clear();
                _selected = null;
                foreach (GuideTopic topic in Topics())
                {
                    var row = new Row { Topic = topic };
                    Button button = _ui.Button("Topic", _list, topic.Title, () => Select(row), 42f, 19f);
                    row.Object = button.gameObject;
                    row.Plate = button.GetComponent<Image>();
                    TextMeshProUGUI label = button.GetComponentInChildren<TextMeshProUGUI>(true);
                    label.alignment = TextAlignmentOptions.MidlineLeft;
                    string body = string.Empty;
                    try
                    {
                        body = topic.Body() ?? string.Empty;
                    }
                    catch (Exception e)
                    {
                        MetaLog.Error(e);
                    }
                    row.Searchable = topic.Title + "\n" + body;
                    _rows.Add(row);
                }
                _query.Length = 0;
                Filter();
                Row first = _rows.Count > 0 ? _rows[0] : null;
                foreach (Row row in _rows)
                {
                    if (row.Topic.Id == keep)
                    {
                        first = row;
                    }
                }
                if (first != null)
                {
                    Select(first);
                }
            }

            private void Select(Row row)
            {
                if (_selected != null && _selected.Plate != null)
                {
                    _selected.Plate.color = MetaUi.Plate;
                }
                _selected = row;
                row.Plate.color = new Color(0.45f, 0.27f, 0.05f, 1f);
                string text;
                try
                {
                    text = "<size=140%><b>" + row.Topic.Title + "</b></size>\n\n" + Render(row.Topic);
                }
                catch (Exception e)
                {
                    MetaLog.Error(e);
                    text = row.Topic.Title;
                }
                _page.text = text;
                KeyScroll.velocity = Vector2.zero;
                KeyScroll.verticalNormalizedPosition = 1f;
            }

            // Search as you type: there is no input box, the letters typed while the guide is open are the search.
            public override void Tick()
            {
                string typed = Keys.PanelTyped();
                if (string.IsNullOrEmpty(typed))
                {
                    return;
                }
                bool changed = false;
                foreach (char c in typed)
                {
                    if (c == '\b')
                    {
                        if (_query.Length > 0)
                        {
                            _query.Length--;
                            changed = true;
                        }
                    }
                    else if (!char.IsControl(c) && _query.Length < 30 && (_query.Length > 0 || c != ' '))
                    {
                        _query.Append(c);
                        changed = true;
                    }
                }
                if (changed)
                {
                    Filter();
                }
            }

            /// <summary>Esc clears a search first, and closes the guide when there is none.</summary>
            public override bool Escape()
            {
                if (_query.Length == 0)
                {
                    return false;
                }
                _query.Length = 0;
                Filter();
                return true;
            }

            private void Filter()
            {
                string query = _query.ToString();
                int shown = 0;
                foreach (Row row in _rows)
                {
                    bool match = query.Length == 0 || row.Searchable.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
                    row.Object.SetActive(match);
                    shown += match ? 1 : 0;
                }
                _search.text = query.Length == 0
                    ? "reallife_guide_search_hint".GetLocalization()
                    : "reallife_guide_search".GetLocalization().Replace("{query}", query).Replace("{count}", shown.ToString(CultureInfo.InvariantCulture));
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

        private static Sprite _icon;   // kept for the session: the game looks contact icons up by sprite name

        /// <summary>Returns true if the contact is in the save after the call.</summary>
        public static bool Setup()
        {
            if (!Settings.PhoneContacts || SaveGameManager.Current == null)
            {
                return false;
            }
            try
            {
                AddIcon();
                Contact contact = Contact.GetContact(ContactId, ContactCategoryName.General, "reallife_contact_guide_desc", null, false, true);
                var dialogType = (CallDialogType)BAModAPI.ModEnumHash.GetSafeHash("reallife_guide_calldialogtype");
                contact.callDialogTypeOverride = dialogType;   // not saved by the game, so set on every load
                CallDialogFactory.RegisterDialog(dialogType, () => new GuideDialog());
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

        // A plain disc with a white middle, like the mod's other contacts.
        private static void AddIcon()
        {
            var references = InstanceBehavior<GlobalReferences>.Instance;
            if (references == null || references.contactIcons == null)
            {
                return;
            }
            foreach (Sprite existing in references.contactIcons)
            {
                if (existing != null && existing.name == ContactId)
                {
                    return;
                }
            }
            if (_icon == null)
            {
                const int size = 96;
                var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
                var pixels = new Color[size * size];
                float radius = size / 2f - 2f;
                var colour = new Color(1f, 0.58f, 0.08f);
                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        float distance = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(size / 2f, size / 2f));
                        Color pixel = distance < radius * 0.42f ? Color.white : colour;
                        pixel.a = Mathf.Clamp01(radius - distance);
                        pixels[y * size + x] = pixel;
                    }
                }
                texture.SetPixels(pixels);
                texture.Apply();
                _icon = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
                _icon.name = ContactId;
            }
            Sprite[] old = references.contactIcons;
            var icons = new Sprite[old.Length + 1];
            old.CopyTo(icons, 0);
            icons[old.Length] = _icon;
            references.contactIcons = icons;
        }
    }

    /// <summary>The call to the guide contact: one line, then the guide opens.</summary>
    public class GuideDialog : Dialog
    {
        public GuideDialog()
        {
            npcNameKey = "reallife-guide";
            DialogController.current.ShowEntry(PhoneContacts.Closing("reallife_guide_call"));
            Guide.OpenSoon();
        }
    }
}
