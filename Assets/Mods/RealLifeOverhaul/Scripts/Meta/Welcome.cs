using System;
using System.Collections.Generic;
using System.Text;
using Localizor;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RealLifeOverhaul.Meta
{
    /// <summary>
    /// The welcome panel: shown once on the first city load with the mod on this computer, and once more after
    /// each change of version (with what changed since the version last seen); otherwise only from its button on
    /// the options page. It offers the four presets and the guide. The version last seen is kept in PlayerPrefs
    /// with the mod's options, so it is per install, not per save. A version seen that is not older than the one
    /// running reads as a first run: there is nothing to list as new.
    /// </summary>
    public static class Welcome
    {
        private const string SeenId = "meta_seen_release";
        private const string DevelopmentSeenId = "meta_seen_version";   // written by the builds before the first release (0.13 to 0.46): dropped, never read

        private static WelcomePanel _panel;

        /// <summary>The version whose welcome the player has seen, or null.</summary>
        public static string SeenVersion
        {
            get
            {
                string key = Settings.PrefsKey(SeenId);
                string seen = key == null ? null : UnityEngine.PlayerPrefs.GetString(key, string.Empty);
                return string.IsNullOrEmpty(seen) ? null : seen;
            }
        }

        /// <summary>True when this version's welcome has not been shown yet.</summary>
        public static bool Due => Settings.ModId != null && SeenVersion != Changelog.Version;

        /// <summary>Shows the panel. <paramref name="asked"/>: from the options page, so it reads as on a first run.</summary>
        public static void Open(bool asked)
        {
            try
            {
                string seen = SeenVersion;
                string key = Settings.PrefsKey(SeenId);
                if (key != null)
                {
                    UnityEngine.PlayerPrefs.DeleteKey(Settings.PrefsKey(DevelopmentSeenId));
                    UnityEngine.PlayerPrefs.SetString(key, Changelog.Version);
                    UnityEngine.PlayerPrefs.Save();
                }
                MetaUi ui = MetaUi.Instance;
                if (_panel == null || _panel.Root == null)
                {
                    _panel = new WelcomePanel(ui);
                }
                bool update = !asked && seen != null && Changelog.Compare(seen, Changelog.Version) < 0;
                _panel.Seen = update ? seen : null;
                _panel.Refresh();
                ui.Show(_panel);
                MetaLog.Info("welcome shown (" + (asked ? "asked for" : update ? "after " + seen : "first run") + ").");
            }
            catch (Exception e)
            {
                MetaLog.Error(e);
            }
        }

        private sealed class WelcomePanel : MetaPanel
        {
            public string Seen;

            private readonly MetaUi _ui;
            private readonly TextMeshProUGUI _text;
            private readonly TextMeshProUGUI[] _presetLabels = new TextMeshProUGUI[4];

            public WelcomePanel(MetaUi ui)
            {
                _ui = ui;
                Root = ui.Frame("Welcome", new Vector2(1120f, 760f), out RectTransform window);
                ui.TitleBar(window, "reallife_meta_welcome_title".GetLocalization().Replace("{version}", Changelog.Version), out TextMeshProUGUI _);

                KeyScroll = ui.Scroll("Body", window, out RectTransform content, 8f);
                MetaUi.Stretch((RectTransform)KeyScroll.transform, 18f, 150f, 18f, 70f);
                _text = ui.Text("Text", content, 21f, MetaUi.Ink, TextAlignmentOptions.TopLeft, true);
                _text.lineSpacing = 6f;

                RectTransform presets = Line(window, 82f);
                for (int i = 0; i < 4; i++)
                {
                    var intensity = (Intensity)i;
                    Button button = ui.Button("Preset", presets, string.Empty, () => Pick(intensity));
                    _presetLabels[i] = button.GetComponentInChildren<TextMeshProUGUI>(true);
                }

                RectTransform bottom = Line(window, 16f);
                ui.Button("Guide", bottom, "reallife_meta_welcome_guide".GetLocalization(), () =>
                {
                    _ui.Close(this);
                    Guide.Open();
                });
                ui.Button("Close", bottom, "reallife_meta_welcome_close".GetLocalization(), () => _ui.Close(this));
            }

            private RectTransform Line(RectTransform window, float fromBottom)
            {
                GameObject row = _ui.New("Row", window);
                var rect = (RectTransform)row.transform;
                rect.anchorMin = new Vector2(0f, 0f);
                rect.anchorMax = new Vector2(1f, 0f);
                rect.pivot = new Vector2(0.5f, 0f);
                rect.offsetMin = new Vector2(18f, fromBottom);
                rect.offsetMax = new Vector2(-18f, fromBottom + 54f);
                var layout = row.AddComponent<HorizontalLayoutGroup>();
                layout.childControlWidth = true;
                layout.childControlHeight = true;
                layout.childForceExpandWidth = true;
                layout.childForceExpandHeight = true;
                layout.spacing = 14f;
                return rect;
            }

            public void Refresh()
            {
                var text = new StringBuilder();
                text.Append((Seen == null ? "reallife_meta_welcome_first" : "reallife_meta_welcome_update").GetLocalization()
                    .Replace("{version}", Changelog.Version).Replace("{seen}", Seen ?? string.Empty));
                if (Seen != null)
                {
                    string since = Changelog.Since(Seen);
                    if (since.Length > 0)
                    {
                        text.Append("\n\n").Append(since);
                    }
                }
                text.Append("\n\n").Append("reallife_meta_welcome_presets".GetLocalization());
                text.Append("\n\n").Append("reallife_meta_welcome_pointer".GetLocalization());
                _text.text = Guide.Fill(text.ToString());

                Intensity? now = Presets.Detect();
                for (int i = 0; i < 4; i++)
                {
                    string label = Presets.NameKey((Intensity)i).GetLocalization();
                    _presetLabels[i].text = now.HasValue && (int)now.Value == i ? "<b>" + label + "</b>  " + "reallife_meta_welcome_current".GetLocalization() : label;
                }
            }

            private void Pick(Intensity intensity)
            {
                string result = Presets.Apply(intensity);
                Refresh();
                MetaUi.Message(Presets.NameKey(intensity).GetLocalization(), result, new KeyValuePair<string, Action>[0]);
            }
        }
    }
}
