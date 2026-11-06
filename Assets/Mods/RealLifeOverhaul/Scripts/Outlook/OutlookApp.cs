using System;
using System.Collections.Generic;
using System.Globalization;
using BAModAPI;
using RealLifeOverhaul.Economy;
using RealLifeOverhaul.GameUi;
using TMPro;
using UI.Smartphone;
using UnityEngine;
using UnityEngine.UI;

namespace RealLifeOverhaul.Outlook
{
    /// <summary>
    /// The Economy app, drawn in its own look inside the game's phone (docs/features/economy-app.md): a header with the verdict and
    /// the date, seven tabs, and the dashboard of the tab showing, in cards, charts and advice. Dark to begin with, light by the
    /// option. Everything is made once and written again when the app opens, a tab is picked, each game hour while it is open, or
    /// the look is changed; nothing runs per frame.
    /// </summary>
    internal sealed partial class OutlookApp
    {
        public static IModLogger Logger;

        private const float Margin = 64f, Gap = 32f, HeadHeight = 150f, TabsHeight = 76f;

        private readonly OutlookDirector _director;
        private readonly OutlookReadings _r = new OutlookReadings();
        private readonly List<OutlookInsight> _advice = new List<OutlookInsight>(OutlookCatalogue.InsightsMost);
        private readonly int[] _window = new int[OutlookCatalogue.HistoryDays];
        private readonly int[] _order = new int[OutlookReadings.MostDistricts];

        private GameObject _rootObject;
        private RectTransform _root;
        private Image _back;
        private OutlookPage _head, _body;
        private int _tab, _wanted = -1;
        private float _laidWidth = -1f, _laidHeight = -1f;
        private bool _built, _writing;
        private OutlookVerdict _verdict = OutlookVerdict.Steady;

        /// <summary>What the last write drew, for the in-game test: the tab, its cards, lettering, pictures with a line in them and clicks.</summary>
        internal struct Drawn
        {
            public int Tab, Cards, Words, Pictures, Clicks, Readings, Charts;
        }

        public OutlookApp(OutlookDirector director)
        {
            _director = director;
        }

        internal bool Showing => _rootObject != null && _rootObject.activeInHierarchy;
        internal int Tab => _tab;
        internal OutlookReadings Readings => _r;
        internal List<OutlookInsight> Advice => _advice;
        internal OutlookVerdict Verdict => _verdict;
        internal Drawn Last { get; private set; }

        /// <summary>The app's panel, asleep; null, logged, if the game's parts are not as expected.</summary>
        public GameObject Build(FullMenu menu, Transform dormant)
        {
            using var piece = SessionWatch.Piece("economy app: the app being built in the game's frame");   // once a city: a few objects and the shapes' textures
            try
            {
                var go = new GameObject(OutlookDirector.AppId, typeof(RectTransform));
                go.layer = dormant.gameObject.layer;
                go.transform.SetParent(dormant, false);
                _rootObject = go;
                _root = (RectTransform)go.transform;
                OutlookKit.Stretch(_root, 0f, 0f, 0f, 0f);
                _back = go.AddComponent<Image>();   // the page's ground; takes the clicks the menu behind it would get
                _back.color = OutlookKit.C(OutlookTheme.Dark.Back);
                _back.raycastTarget = true;
                _head = new OutlookPage(_root, "Header", false);
                _body = new OutlookPage(_root, "Body", true);
                _body.AddScrollBar();
                _built = true;
                return go;
            }
            catch (Exception e)
            {
                Logger?.Error(e);
                UiClone.Fallback("the Economy app could not be built");
                return null;
            }
        }

        public void Dispose()
        {
            _built = false;
        }

        /// <summary>The tab to show when the app opens (a notice or the Finance app asked).</summary>
        public void Want(int tab)
        {
            _wanted = OutlookTab.Clamp(tab);
            if (Showing)
            {
                Opened();
            }
        }

        public void Opened()
        {
            if (!_built)
            {
                return;
            }
            if (_wanted >= 0)
            {
                _tab = _wanted;
                _wanted = -1;
                _body.ScrollToTop();
            }
            Refresh();
        }

        public void Closed()
        {
            Save();
        }

        /// <summary>A game hour has passed: the open page is written again.</summary>
        public void Hour()
        {
            if (_built && Showing && !_writing)
            {
                Refresh();
            }
        }

        private void PickTab(int tab)
        {
            if (tab == _tab)
            {
                return;
            }
            _tab = tab;
            _body.ScrollToTop();
            Refresh();
        }

        // ------------------------------------------------------------------ the write

        /// <summary>Reads the game, works out what the pages need, and writes the header, the tabs and the tab showing.</summary>
        public void Refresh()
        {
            if (!_built || _writing)
            {
                return;
            }
            _writing = true;
            float started = Time.realtimeSinceStartup;
            try
            {
                OutlookPalette palette = OutlookTheme.Of(Settings.EconomyAppLook);
                _back.color = OutlookKit.C(palette.Back);
                bool read = _director.Look(_r);
                if (read)
                {
                    Study();
                }
                Lay(out float width, out float height);
                WriteHead(palette, width, read);
                WriteBody(palette, width, height, read);
                if (Meta.Dev.Verbose)
                {
                    Logger?.Info("Economy app: tab " + _tab + " written, " + _body.CardsDrawn + " card(s), " + _body.WordsDrawn + " text(s), " + _body.PicturesDrawn + " picture(s); "
                                 + ((Time.realtimeSinceStartup - started) * 1000f).ToString("0.0", CultureInfo.InvariantCulture) + " ms.");
                }
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
            finally
            {
                _writing = false;
            }
        }

        // The trends, the verdict and the advice, from the readings and the history.
        private void Study()
        {
            _verdict = OutlookStudy.Run(_director.Book, _r, _window);
            OutlookInsights.Build(_r, _advice);
        }

        // The size of the page; the first time it may not be laid out yet.
        private void Lay(out float width, out float height)
        {
            width = _root.rect.width;
            height = _root.rect.height;
            if (width < 100f || height < 100f)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(_root);
                width = _root.rect.width;
                height = _root.rect.height;
            }
            if (Mathf.Abs(width - _laidWidth) >= 1f || Mathf.Abs(height - _laidHeight) >= 1f)
            {
                _laidWidth = width;
                _laidHeight = height;
                OutlookKit.Place(_head.Root, 0f, 0f, width, HeadHeight + TabsHeight + 20f);
                OutlookKit.Place(_body.Root, 0f, HeadHeight + TabsHeight + 24f, width, Mathf.Max(100f, height - HeadHeight - TabsHeight - 24f));
            }
        }

        // ------------------------------------------------------------------ the header and the tabs

        private void WriteHead(OutlookPalette p, float width, bool read)
        {
            _head.Begin(p);
            // A glow behind the title, the accent fading into the ground.
            OutlookMesh glow = _head.Picture(0f, 0f, width, HeadHeight + TabsHeight + 20f);
            glow.Fade(0f, 0f, width, HeadHeight + 40f, OutlookKit.C32(p.Accent, p.Dark ? 0.10f : 0.07f), OutlookKit.C32(p.Back, 0f));
            glow.End();

            string title = OutlookText.T("reallife_econapp_app");
            float titleWidth = _head.Measure(title, 66f, true);
            _head.Text(title, Margin, 30f, titleWidth + 20f, 84f, 66f, p.Text, TextAlignmentOptions.MidlineLeft, true);

            // The verdict, as a coloured chip beside the title.
            float x = Margin + titleWidth + 40f;
            if (read && _r.CityOn)
            {
                string word = OutlookText.Words.VerdictWord(_verdict);
                Tint(p, _verdict, out int ink, out int fill);
                float w = _head.Measure(word, 32f, true) + 92f;
                _head.Pill(x, 44f, w, 58f, fill);
                _head.Pill(x + 24f, 44f + 20f, 18f, 18f, ink);
                _head.Text(word, x + 58f, 44f, w - 70f, 58f, 32f, ink, TextAlignmentOptions.MidlineLeft, true);
            }
            else if (!_director.CityRuns)
            {
                string off = OutlookText.T("reallife_econapp_city_off_chip");
                float w = _head.Measure(off, 28f, true) + 56f;
                _head.Pill(x, 46f, w, 54f, p.Flat);
                _head.Text(off, x + 28f, 46f, w - 40f, 54f, 28f, p.Muted, TextAlignmentOptions.MidlineLeft, true);
            }

            // The date, and the switch between the looks, at the right.
            float right = width - Margin;
            if (OptionsHaveLook())
            {
                _head.Pill(right - 66f, 42f, 66f, 66f, p.Raised);
                WriteSwitch(p, right - 66f, 42f, 66f);
                _head.Click(right - 66f, 42f, 66f, 66f, SwitchLook);
                right -= 66f + 20f;
            }
            string date = read ? OutlookText.Date(_r.Day) : string.Empty;
            if (date.Length > 0)
            {
                float w = _head.Measure(date, 30f, false) + 64f;
                _head.Pill(right - w, 42f, w, 66f, p.Raised);
                _head.Text(date, right - w, 42f, w, 66f, 30f, p.Muted, TextAlignmentOptions.Midline);
            }

            // The tabs: a track of pills, the one showing filled with the accent.
            float[] widths = new float[OutlookTab.Count];
            float total = 0f;
            for (int i = 0; i < widths.Length; i++)
            {
                widths[i] = _head.Measure(OutlookText.T(OutlookTab.Keys[i]), 30f, true) + 64f;
                total += widths[i];
            }
            float trackWidth = total + 16f;
            if (trackWidth > width - Margin * 2f)
            {
                float shrink = (width - Margin * 2f - 16f) / total;
                for (int i = 0; i < widths.Length; i++)
                {
                    widths[i] *= shrink;
                }
                trackWidth = width - Margin * 2f;
            }
            _head.Box(Margin, HeadHeight, trackWidth, TabsHeight, p.Card, TabsHeight * 0.5f);
            float tx = Margin + 8f;
            for (int i = 0; i < OutlookTab.Count; i++)
            {
                bool on = i == _tab;
                if (on)
                {
                    _head.Pill(tx, HeadHeight + 8f, widths[i], TabsHeight - 16f, p.Accent);
                }
                _head.Text(OutlookText.T(OutlookTab.Keys[i]), tx, HeadHeight + 8f, widths[i], TabsHeight - 16f, 30f, on ? p.AccentInk : p.Muted, TextAlignmentOptions.Midline, true);
                int tab = i;
                _head.Click(tx, HeadHeight + 8f, widths[i], TabsHeight - 16f, () => PickTab(tab));
                tx += widths[i];
            }
            if (_director.SampleShown)
            {
                string sample = OutlookText.T("reallife_econapp_sample_chip");
                float w = _head.Measure(sample, 26f, true) + 48f;
                _head.Pill(Margin + trackWidth + 24f, HeadHeight + 10f, w, TabsHeight - 20f, p.WarnSoft);
                _head.Text(sample, Margin + trackWidth + 24f, HeadHeight + 10f, w, TabsHeight - 20f, 26f, p.Warn, TextAlignmentOptions.Midline, true);
            }
            _head.End(0f);
        }

        // The ink and the fill of a pill for a way a figure reads.
        private static void Tint(OutlookPalette p, OutlookTone tone, out int ink, out int fill)
        {
            switch (tone)
            {
                case OutlookTone.Good:
                    ink = p.Good;
                    fill = p.GoodSoft;
                    break;
                case OutlookTone.Bad:
                    ink = p.Bad;
                    fill = p.BadSoft;
                    break;
                case OutlookTone.Neutral:
                    ink = p.Info;
                    fill = p.InfoSoft;
                    break;
                default:
                    ink = p.Muted;
                    fill = p.Flat;
                    break;
            }
        }

        // ------------------------------------------------------------------ the look

        private static bool OptionsHaveLook()
        {
            return Meta.OptionsLayout.AppFind(Settings.EconomyAppLookId) != null;
        }

        // The button names the look a press gives: a sun while the app is dark, a moon while it is light.
        private void WriteSwitch(OutlookPalette p, float x, float y, float size)
        {
            OutlookMesh icon = _head.Picture(x, y, size, size);
            float c = size * 0.5f;
            if (p.Dark)
            {
                icon.Disc(c, c, 10f, OutlookKit.C32(p.Warn));
                for (int i = 0; i < 8; i++)
                {
                    float a = i * Mathf.PI / 4f;
                    icon.Triangle(c + Mathf.Cos(a) * 15f - Mathf.Sin(a) * 2.6f, c + Mathf.Sin(a) * 15f + Mathf.Cos(a) * 2.6f,
                        c + Mathf.Cos(a) * 15f + Mathf.Sin(a) * 2.6f, c + Mathf.Sin(a) * 15f - Mathf.Cos(a) * 2.6f, c + Mathf.Cos(a) * 22f, c + Mathf.Sin(a) * 22f, OutlookKit.C32(p.Warn));
                }
            }
            else
            {
                icon.Disc(c, c, 14f, OutlookKit.C32(p.Info));
                icon.Disc(c + 8f, c - 6f, 12f, OutlookKit.C32(p.Raised));
            }
            icon.End();
        }

        private void SwitchLook()
        {
            int next = OutlookTheme.Of(Settings.EconomyAppLook).Dark ? OutlookTheme.LightLook : OutlookTheme.DarkLook;
            if (!Meta.OptionsLayout.AppSet(Meta.OptionsLayout.AppFind(Settings.EconomyAppLookId), next))
            {
                return;
            }
            _themeWritten = true;
            Refresh();
        }

        private bool _themeWritten;

        /// <summary>What the switch wrote goes on disk as the app closes, as the Settings app puts its changes.</summary>
        public void Save()
        {
            if (_themeWritten)
            {
                _themeWritten = false;
                UiClone.Safe(UnityEngine.PlayerPrefs.Save);
            }
        }
    }
}
