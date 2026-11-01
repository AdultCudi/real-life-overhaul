using System;
using UnityEngine;

namespace RealLifeOverhaul.GameUi
{
    /// <summary>
    /// The pager stepping aside for another mod that pages the same grid (PagerAsideRules), other mods' own buttons shown
    /// again joining the pages, and a phone another mod made taller than the screen kept on it while this pager stands aside.
    /// </summary>
    public sealed partial class PhoneGridPager
    {
        private const int FrameMost = 6;                              // the grid and its parents up to the phone's canvas: the parts another mod may make taller
        private const string VisitorName = "RealLife Visiting Button";   // the test's stand-in for another mod's button

        private string _asideWhy;                // what showed another mod pages the grid, for the line in Player.log
        private float _lastShown = float.NaN;    // when other mods' buttons were last found shown again on pages not showing
        private int _shownRun;                   // ...and how many passes in a row found them so (PagerAsideRules.Run)
        private RectTransform[] _frame;          // the HUD phone's grid and its parents, outermost last
        private float[] _frameHeight;            // ...their heights as the game built them, read when the pager came
        private RectTransform _screen;           // the phone's canvas, as large as the screen
        private static bool _fitTold;

        /// <summary>Other mods' buttons shown again by their own mods that joined the pages, in the session.</summary>
        internal static int Joined { get; private set; }

        // Another mod pages this grid: a known pager's script on it, or a button of the game's or this mod's shown on a page not
        // showing. Other mods' buttons shown again by their own mods join the pages (their marks taken off; Apply pages them).
        private bool SomeoneElse()
        {
            int own = 0, others = 0;
            Transform first = null;
            for (int i = 0; i < transform.childCount; i++)
            {
                Transform child = transform.GetChild(i);
                if (!child.gameObject.activeSelf || !PagedOff(child))
                {
                    continue;
                }
                if (_gameButtons.Contains(child) || RankOf(child) >= 0)
                {
                    own++;
                    first = own == 1 ? child : first;
                }
                else
                {
                    others++;
                    first = own == 0 && others == 1 ? child : first;
                }
            }
            bool pager = PagerOnGrid(out Type script);
            if (others > 0)
            {
                _shownRun = PagerAsideRules.Run(Time.unscaledTime, _lastShown, _shownRun);
                _lastShown = Time.unscaledTime;
            }
            int verdict = PagerAsideRules.Verdict(own, others, _shownRun, pager);
            if (verdict == PagerAsideRules.Aside)
            {
                _asideWhy = pager ? script.FullName + " on it"
                    : own > 0 ? "it showed '" + first.name + "', which was on a page not showing"
                    : "it showed its own button '" + first.name + "' again on " + _shownRun + " passes in a row, faster than pages can follow";
                return true;
            }
            if (verdict == PagerAsideRules.Join)
            {
                for (int i = 0; i < transform.childCount; i++)
                {
                    Transform child = transform.GetChild(i);
                    if (child.gameObject.activeSelf && PagedOff(child))
                    {
                        child.GetComponent<PageMark>().Off = false;   // shown by its own mod: one of the pages' items again
                        Joined++;
                    }
                }
                if (Meta.Dev.Verbose)
                {
                    Logger?.Info("Game UI: another mod showed its own button '" + first.name + "' again on " + (_strip ? "the full menu's row" : "the HUD phone") + "; it joins the pages.");
                }
            }
            return false;
        }

        // A button's place among the mod's apps, -1 for the game's and other mods' (read once from its name).
        private int RankOf(Transform button)
        {
            if (!_ranks.TryGetValue(button, out int rank))
            {
                rank = PagerRules.ModRank(button.name);
                _ranks[button] = rank;
            }
            return rank;
        }

        private bool PagerOnGrid(out Type found)
        {
            found = null;
            _scripts.Clear();
            GetComponents(_scripts);
            for (int i = 0; i < _scripts.Count; i++)
            {
                MonoBehaviour script = _scripts[i];
                if (script != null && script != this && Paging(script.GetType()))
                {
                    found = script.GetType();
                    return true;
                }
            }
            return false;
        }

        // Worked out once per script type (its full name is made each time it is asked).
        private static bool Paging(Type type)
        {
            if (KnownScripts.TryGetValue(type, out bool pager))
            {
                return pager;
            }
            pager = type.Assembly != typeof(PhoneGridPager).Assembly && PagerAsideRules.Pager(type.FullName);
            KnownScripts[type] = pager;
            return pager;
        }

        private void StepAside()
        {
            _aside = true;
            ShowAll();
            RestoreRow();
            DropTiles();
            DropFolders();
            UiClone.Fallback((_strip ? "another mod pages the full menu's app buttons" : "another mod pages the phone's home screen") + " (" + (_asideWhy ?? "an error, above")
                             + "), so this one leaves the pages to it");
            FitLater();
        }

        private void ShowAll()
        {
            for (int i = 0; i < transform.childCount; i++)
            {
                Transform child = transform.GetChild(i);
                var mark = child.GetComponent<PageMark>();
                if (mark == null || !mark.Off)
                {
                    continue;
                }
                mark.Off = false;
                child.gameObject.SetActive(true);
            }
        }

        // Stepping aside, or the pager gone.
        private void DropFolders()
        {
            _open = -1;
            _foldered = false;
            if (_folders != null)
            {
                _folders.Destroy();
                _folders = null;
            }
            if (_keys != null)
            {
                _keys.enabled = false;
            }
        }

        // Once, when the HUD phone's pager comes: the grid and its parents up to the phone's canvas with their heights as built
        // (Hotelier and Restaurateur grows the phone once it sees more buttons than its four rows), each watched for a change.
        private void ReadFrame()
        {
            if (_strip || _frame != null)
            {
                return;
            }
            int count = 0;
            var parts = new RectTransform[FrameMost];
            for (Transform part = transform; part != null && count < FrameMost; part = part.parent)
            {
                if (part.GetComponent<Canvas>() != null)
                {
                    _screen = part as RectTransform;
                    break;
                }
                if (part is RectTransform rect)
                {
                    parts[count++] = rect;
                }
            }
            if (_screen == null || count == 0)
            {
                return;   // not under its canvas as the game builds it: nothing to keep on the screen
            }
            _frame = new RectTransform[count];
            _frameHeight = new float[count];
            for (int i = 0; i < count; i++)
            {
                _frame[i] = parts[i];
                _frameHeight[i] = parts[i].rect.height;
                if (i > 0)
                {
                    PhoneFrameWatch watch = parts[i].GetComponent<PhoneFrameWatch>() ?? parts[i].gameObject.AddComponent<PhoneFrameWatch>();
                    watch.Pager = this;   // the grid itself tells through its own OnRectTransformDimensionsChange
                }
            }
        }

        // Stood aside on the HUD phone: one look before the canvas is drawn at whether the phone still fits the screen.
        private void FitLater()
        {
            if (_strip || _frame == null || _pending || !isActiveAndEnabled)
            {
                return;
            }
            _pending = true;
            Canvas.willRenderCanvases += _late;
        }

        // Every part another mod made taller that runs past the screen's top or bottom gives back what it gained, as far as it
        // runs past, outermost first (the parts inside follow it). Told once in Player.log.
        private void Fit()
        {
            if (_strip || _frame == null || _screen == null)
            {
                return;
            }
            _screen.GetWorldCorners(Corners);
            float bottom = Corners[0].y, top = Corners[1].y;
            bool fitted = false;
            for (int i = _frame.Length - 1; i >= 0; i--)
            {
                RectTransform part = _frame[i];
                float scale = part != null ? part.lossyScale.y : 0f;
                if (!(scale > 0f))
                {
                    continue;
                }
                part.GetWorldCorners(Corners);
                float height = part.rect.height;
                float wanted = PagerAsideRules.FitHeight(height, _frameHeight[i], part.pivot.y, (Corners[1].y - top) / scale, (bottom - Corners[0].y) / scale);
                if (wanted < height)
                {
                    part.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, wanted);
                    fitted = true;
                }
            }
            if (fitted && !_fitTold)
            {
                _fitTold = true;
                Logger?.Info("Game UI: another mod made the phone taller than the screen while it pages the home screen; the phone is kept within the screen.");
            }
        }

        /// <summary>
        /// Test: a stand-in for another mod's button joins the grid, is paged off, and is shown again as its own mod would
        /// show it. "PASS" when the pager pages it and does not step aside; the stand-in is then taken away. Else what is wrong.
        /// </summary>
        internal string VisitorCheck()
        {
            if (_aside)
            {
                return "another mod pages this grid, so no stand-in was shown: PASS";
            }
            if (_template == null)
            {
                return "no button to copy: FAIL";
            }
            int page = _page;
            GameObject visitor = Instantiate(_template.gameObject, transform, false);
            visitor.name = VisitorName;
            visitor.tag = "Untagged";
            string said;
            try
            {
                visitor.SetActive(true);   // as another mod puts its button on
                Refresh();
                int at = _items.IndexOf(visitor.transform);
                if (at < 0 || Pages < 2)
                {
                    said = "it joined " + (at < 0 ? "no page (in a closed folder's place)" : "the only page") + ", so it could not be shown on a page not showing: PASS";
                }
                else
                {
                    Turn(PageOf(at) == 0 ? 1 : 0);
                    bool pagedOff = !visitor.activeSelf && PagedOff(visitor.transform);
                    visitor.SetActive(true);   // its own mod shows it again
                    Refresh();
                    bool joined = !_aside && !visitor.activeSelf && PagedOff(visitor.transform);
                    said = "paged off " + (pagedOff ? "yes" : "no") + ", shown again by its mod and " + (_aside ? "this pager stepped aside" : joined ? "paged off again" : "left showing")
                           + ": " + (pagedOff && joined ? "PASS" : "FAIL");
                }
            }
            finally
            {
                visitor.SetActive(false);
                visitor.transform.SetParent(null, false);
                Destroy(visitor);
                if (!_aside)
                {
                    Turn(page);
                    Refresh();
                }
            }
            return said;
        }
    }

    /// <summary>On the HUD phone's parts above its grid: a change of their size has the pager look again whether the phone fits the screen. Costs nothing a frame.</summary>
    public sealed class PhoneFrameWatch : MonoBehaviour
    {
        internal PhoneGridPager Pager;

        private void OnRectTransformDimensionsChange()
        {
            if (Pager != null)
            {
                Pager.Changed();
            }
        }
    }
}
