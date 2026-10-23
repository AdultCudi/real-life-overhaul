using System;
using BAModAPI;
using RealLifeOverhaul.GameUi;
using TMPro;
using UI;
using UnityEngine;
using UnityEngine.UI;

namespace RealLifeOverhaul
{
    /// <summary>
    /// Hygiene, stress and home as three more of the game's needs bars, in a copy of its accessories tab under the top bar,
    /// so it goes wherever the top bar goes. The director calls <see cref="Show"/> once a second; a bar is touched only when
    /// its whole number changes. A fault removes the tab for the session.
    /// </summary>
    public static class HomeNeedsBar
    {
        private const float Pad = 24f;          // from the tab's edge to the first bar, as the game's own tab keeps its content
        private const float NoteWidth = 640f;   // the illness line beside the bars
        private const float Gap = 12f;          // from the open accessories tab

        private static readonly string[] Names = { "Hygiene", "Stress", "Home" };
        private static readonly string[] Words = { "reallife_home_strip_hygiene", "reallife_home_strip_stress", "reallife_home_strip_home" };
        private static readonly string[] Pictures = { "Icon-Person", "Icon-Heart", "Icon-Building" };
        private static readonly string[] PicturesElse = { "Icon-Info-32", "Icon-Info-32", "Icon-Address" };
        private static readonly Color[] Colours =
        {
            new Color(0.33f, 0.74f, 0.96f, 1f), new Color(0.73f, 0.55f, 0.96f, 1f), new Color(0.96f, 0.80f, 0.36f, 1f)
        };

        public static IModLogger Logger;

        private static readonly Image[] Fill = new Image[3];
        private static readonly int[] Shown = { -1, -1, -1 };
        private static GameObject _tab;
        private static RectTransform _tabRect;
        private static RectTransform _accessories;
        private static Component _note;
        private static string _noteShown;
        private static float _alignedX;
        private static float _barsWidth;
        private static bool _on;
        private static bool _noteOn;
        private static bool _beside;
        private static bool _failed;

        /// <param name="note">A line beside the bars (an illness and how long it has to run), or null. Pass the same string object while nothing changes.</param>
        public static void Show(float hygiene, float stress, float home, bool hasHome, string note)
        {
            if (_failed)
            {
                return;
            }
            try
            {
                if (_tab == null)
                {
                    _on = false;
                    if (!Build())
                    {
                        _failed = true;
                        return;
                    }
                }
                Bar(0, hygiene);
                Bar(1, stress);
                Bar(2, hasHome ? home : 0f);
                bool noteOn = note != null;
                bool beside = _accessories != null && _accessories.gameObject.activeSelf;
                if (noteOn != _noteOn || beside != _beside || !_on)
                {
                    _noteOn = noteOn;
                    _beside = beside;
                    Place();
                }
                if (noteOn && !ReferenceEquals(note, _noteShown))
                {
                    _noteShown = note;
                    UiClone.Words(_note, note);
                }
                if (!_on)
                {
                    _on = true;
                    _tab.SetActive(true);
                    if (Meta.Dev.Verbose)
                    {
                        Logger?.Info("Home check (needs bars): shown under the game's top bar, " + (beside ? "beside the open accessories tab" : "under the game's three bars")
                                     + ": hygiene " + Shown[0] + ", stress " + Shown[1] + ", home " + Shown[2] + (noteOn ? ", with the illness line" : string.Empty) + ".");
                    }
                }
            }
            catch (Exception e)
            {
                Fail(e);
            }
        }

        public static void Hide()
        {
            if (_on)
            {
                _on = false;
                if (_tab != null)
                {
                    _tab.SetActive(false);
                }
                if (Meta.Dev.Verbose)
                {
                    Logger?.Info("Home check (needs bars): hidden (not at home, no need in its warning zone and not ill, or a screen is open).");
                }
            }
        }

        /// <summary>The tab gone, and the session's refusal forgotten: when the home director goes with the city.</summary>
        public static void Remove()
        {
            if (_tab != null)
            {
                UnityEngine.Object.Destroy(_tab);
            }
            Forget();
            _failed = false;
        }

        private static void Forget()
        {
            _tab = null;
            _tabRect = null;
            _accessories = null;
            _note = null;
            _noteShown = null;
            _on = false;
            _noteOn = false;
            for (int i = 0; i < 3; i++)
            {
                Fill[i] = null;
                Shown[i] = -1;
            }
        }

        private static void Fail(Exception e)
        {
            _failed = true;
            Logger?.Error(e);
            Logger?.Warn("Home: the needs bars are off for this session after the error above; the Wellbeing contact still gives the figures.");
            try
            {
                if (_tab != null)
                {
                    UnityEngine.Object.Destroy(_tab);
                }
            }
            catch (Exception again)
            {
                Logger?.Error(again);
            }
            Forget();
        }

        private static void Bar(int index, float value)
        {
            int whole = Mathf.Clamp(Mathf.RoundToInt(value), 0, 100);
            if (whole != Shown[index])
            {
                Shown[index] = whole;
                Fill[index].fillAmount = whole / 100f;
            }
        }

        private static void Place()
        {
            float x = _alignedX;
            if (_beside)
            {
                x = Mathf.Max(x, _accessories.localPosition.x + _accessories.rect.xMax + Gap);
            }
            _tabRect.anchoredPosition = new Vector2(x, _tabRect.anchoredPosition.y);
            _tabRect.sizeDelta = new Vector2(Pad + _barsWidth + Pad + (_noteOn ? NoteWidth : 0f), _tabRect.sizeDelta.y);
            if (_note != null && _note.gameObject.activeSelf != _noteOn)
            {
                _note.gameObject.SetActive(_noteOn);
            }
        }

        private static bool Build()
        {
            UIs uis = InstanceBehavior<UIs>.Instance;
            Transform container = uis != null && uis.topBar != null && uis.topBar.container != null ? uis.topBar.container.transform : null;
            Transform bar = UiClone.Need(container, "Left Bar", "the needs bars");
            Transform stats = UiClone.Need(bar, "PlayerStats", "the needs bars");
            Transform accessories = UiClone.Need(bar, "AccessoriesPanel", "the needs bars");
            Transform word = UiClone.Need(bar, "Date/Label", "the needs bars");
            if (stats == null || accessories == null || word == null || stats.childCount < 3)
            {
                UiClone.Fallback("the game's top bar is not built as expected, so hygiene, stress and home have no bars (the Wellbeing contact still gives the figures)");
                return false;
            }
            using var piece = SessionWatch.Piece("game UI: the needs bars being copied from the game's top bar");
            Transform holder = UiClone.Dormant(bar, "RealLifeOverhaul Needs Holder");
            GameObject tab = null;
            try
            {
                tab = UiClone.Asleep(accessories.gameObject, holder, "RealLifeOverhaul Needs");
                for (int i = tab.transform.childCount - 1; i >= 0; i--)
                {
                    UnityEngine.Object.DestroyImmediate(tab.transform.GetChild(i).gameObject);
                }
                UiClone.StripGameScripts(tab, null);
                Still(tab);

                GameObject copy = UiClone.Asleep(stats.gameObject, tab.transform, "Bars");
                UiClone.StripGameScripts(copy, null);
                var first = (RectTransform)stats.GetChild(0);
                var last = (RectTransform)stats.GetChild(2);
                float firstLeft = first.localPosition.x + first.rect.xMin;
                float lastRight = last.localPosition.x + last.rect.xMax;
                for (int i = 0; i < 3; i++)
                {
                    Transform one = copy.transform.GetChild(i);
                    one.name = Names[i];
                    Still(one.gameObject);
                    Image fill = one.childCount > 0 ? one.GetChild(0).GetComponent<Image>() : null;
                    Image picture = one.childCount > 1 ? one.GetChild(1).GetComponent<Image>() : null;
                    if (fill == null || fill.type != Image.Type.Filled)
                    {
                        UiClone.Fallback("a bar of the game's top bar has no filled picture, so hygiene, stress and home have no bars");
                        UnityEngine.Object.DestroyImmediate(holder.gameObject);
                        return false;
                    }
                    fill.color = Colours[i];
                    fill.raycastTarget = false;
                    Fill[i] = fill;
                    Shown[i] = -1;
                    float textFrom = 20f;
                    if (picture != null)
                    {
                        Sprite sprite = GameSprites.Get(Pictures[i]) ?? GameSprites.Get(PicturesElse[i]);
                        picture.raycastTarget = false;
                        if (sprite != null)
                        {
                            picture.sprite = sprite;
                            textFrom = 56f;
                        }
                        else
                        {
                            picture.gameObject.SetActive(false);   // no picture rather than the picture of another need
                        }
                    }
                    GameObject label = UnityEngine.Object.Instantiate(word.gameObject, one, false);
                    label.name = "Word";
                    var labelRect = (RectTransform)label.transform;
                    labelRect.anchorMin = Vector2.zero;
                    labelRect.anchorMax = Vector2.one;
                    labelRect.pivot = new Vector2(0f, 0.5f);
                    labelRect.offsetMin = new Vector2(textFrom, 0f);
                    labelRect.offsetMax = new Vector2(-18f, 0f);
                    var text = label.GetComponent<TMP_Text>();
                    if (text != null)
                    {
                        text.fontSize = 26f;
                        text.alignment = TextAlignmentOptions.MidlineLeft;
                        text.textWrappingMode = TextWrappingModes.NoWrap;
                        text.overflowMode = TextOverflowModes.Ellipsis;
                        text.raycastTarget = false;
                    }
                    UiClone.Key(label.transform, Words[i]);
                    label.SetActive(true);
                }

                // The copy keeps the game's own spacing; it is moved so that its first bar stands the tab's padding in.
                var copyRect = (RectTransform)copy.transform;
                copyRect.anchorMin = copyRect.anchorMax = new Vector2(0f, 0.5f);
                copyRect.pivot = ((RectTransform)stats).pivot;
                copyRect.sizeDelta = ((RectTransform)stats).rect.size;
                copyRect.anchoredPosition = new Vector2(Pad - firstLeft, 4f);   // (the game's tab keeps its content 4 above its middle: the sprite has a shadow below)
                _barsWidth = lastRight - firstLeft;

                GameObject note = UnityEngine.Object.Instantiate(word.gameObject, tab.transform, false);
                note.name = "Note";
                var noteRect = (RectTransform)note.transform;
                noteRect.anchorMin = noteRect.anchorMax = new Vector2(0f, 0.5f);
                noteRect.pivot = new Vector2(0f, 0.5f);
                noteRect.sizeDelta = new Vector2(NoteWidth - Pad, 60f);
                noteRect.anchoredPosition = new Vector2(Pad + _barsWidth + Pad, 4f);
                var noteText = note.GetComponent<TMP_Text>();
                if (noteText != null)
                {
                    noteText.fontSize = 30f;
                    noteText.alignment = TextAlignmentOptions.MidlineLeft;
                    noteText.textWrappingMode = TextWrappingModes.NoWrap;
                    noteText.overflowMode = TextOverflowModes.Ellipsis;
                    noteText.raycastTarget = false;
                    noteText.color = WorldPanel.Yellow();
                }
                UiClone.Words(note.transform, string.Empty);
                note.SetActive(false);
                _note = note.transform;

                // Under the game's bars: the tab's first bar under the game's first.
                _alignedX = bar.InverseTransformPoint(first.TransformPoint(new Vector3(first.rect.xMin, 0f, 0f))).x - Pad;
                tab.SetActive(false);
                _tabRect = (RectTransform)tab.transform;
                _tabRect.SetParent(bar, false);
                _tabRect.SetSiblingIndex(accessories.GetSiblingIndex() + 1);
                _accessories = (RectTransform)accessories;
                _tab = tab;
                _noteOn = false;
                _noteShown = null;
                UnityEngine.Object.DestroyImmediate(holder.gameObject);
                if (Meta.Dev.Verbose)
                {
                    Logger?.Info("Home check (needs bars): built from the game's top bar: a copy of its accessories tab holding a copy of its three bars (" + _barsWidth.ToString("F0")
                                 + " wide, first bar " + (_alignedX + Pad).ToString("F0") + " from the bar's left edge): PASS.");
                }
                return true;
            }
            catch (Exception e)
            {
                Logger?.Error(e);
                UiClone.Fallback("the needs bars could not be built from the game's top bar");
                if (holder != null)
                {
                    UnityEngine.Object.DestroyImmediate(holder.gameObject);
                }
                if (tab != null)
                {
                    UnityEngine.Object.DestroyImmediate(tab);
                }
                Forget();
                return false;
            }
        }

        // A copy that is looked at and never clicked: no button, and nothing of it takes the pointer from what is under it.
        private static void Still(GameObject copy)
        {
            var button = copy.GetComponent<Button>();
            if (button != null)
            {
                UnityEngine.Object.DestroyImmediate(button);
            }
            var image = copy.GetComponent<Image>();
            if (image != null)
            {
                image.raycastTarget = false;
            }
        }

        /// <summary>The game's pictures this asks for, named before the one look behind the loading screen (GameSprites.Prewarm).</summary>
        internal static void WantPictures()
        {
            GameSprites.Want(Pictures);
            GameSprites.Want(PicturesElse);
        }
    }
}
