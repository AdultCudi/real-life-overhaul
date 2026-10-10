using System;
using System.Collections.Generic;
using BAModAPI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RealLifeOverhaul.GameUi
{
    /// <summary>
    /// Pages for the HUD phone's home screen. The game's grid is two buttons wide and exactly four rows fit above the
    /// phone's lower edge (cells of 250 by 225 in a box 868 high), so its own eight apps fill it: a ninth button has
    /// to go on a second page. The wheel over the grid turns the page, and so do the dots under it.
    ///
    /// Other mods page this grid too, each by switching the buttons on and off. Two doing that would undo each other,
    /// so this one steps aside for good as soon as it sees anyone else at it: a component on the grid that is neither
    /// the game's nor this mod's, or a button it had hidden that someone has shown again. Everything it hid is shown
    /// then, and the other mod's pages are the pages.
    ///
    /// Nothing here runs per frame: it acts when the grid is shown, when its children change, and on the wheel.
    /// </summary>
    public sealed class PhoneGridPager : MonoBehaviour, IScrollHandler
    {
        public const int PerPage = 8;
        private const float DotSize = 14f, DotGap = 14f, DotsFromBottom = 22f;

        public static IModLogger Logger;

        private Transform _template;
        private Sprite _dotSprite;
        private RectTransform _dots;
        private readonly List<Transform> _apps = new List<Transform>(12);
        private readonly List<Transform> _hidden = new List<Transform>(8);
        private readonly List<Transform> _attention = new List<Transform>(2);
        private readonly List<Image> _dotImages = new List<Image>(3);
        private readonly List<MonoBehaviour> _scripts = new List<MonoBehaviour>(4);
        private int _page;
        private bool _aside;        // someone else pages this grid
        private bool _working;      // our own changes must not start another pass
        private bool _ready;        // the template and the dot's picture are known
        private float _lastTurn;

        public int Pages { get; private set; } = 1;
        public int Count => _apps.Count;
        public bool SteppedAside => _aside;

        public static PhoneGridPager On(Transform grid, Transform template, Sprite dot)
        {
            if (grid == null)
            {
                return null;
            }
            PhoneGridPager pager = grid.GetComponent<PhoneGridPager>();
            if (pager == null)
            {
                pager = grid.gameObject.AddComponent<PhoneGridPager>();   // its OnEnable runs here, before it knows the template: it waits
            }
            pager._template = template;
            pager._dotSprite = dot;
            pager._ready = true;
            pager.Refresh();
            return pager;
        }

        private void OnEnable()
        {
            Refresh();
        }

        private void OnTransformChildrenChanged()
        {
            Refresh();
        }

        /// <summary>Where a button is in the grid, counted from 1; 0 if it is not one of the apps.</summary>
        public int PlaceOf(Transform button)
        {
            return _apps.IndexOf(button) + 1;
        }

        /// <summary>Turns to the page a button is on.</summary>
        public void ShowPageOf(Transform button)
        {
            int index = _apps.IndexOf(button);
            if (index >= 0 && !_aside)
            {
                Turn(index / PerPage);
            }
        }

        /// <summary>Marks a button as having something new: the dot of a page that is not showing turns the badge's red.</summary>
        public void SetAttention(Transform button, bool on)
        {
            bool has = _attention.Contains(button);
            if (on == has)
            {
                return;
            }
            if (on)
            {
                _attention.Add(button);
            }
            else
            {
                _attention.Remove(button);
            }
            Paint();
        }

        public void OnScroll(PointerEventData eventData)
        {
            if (_aside || Pages < 2 || Time.unscaledTime - _lastTurn < 0.15f || Mathf.Abs(eventData.scrollDelta.y) < 0.01f)
            {
                return;
            }
            _lastTurn = Time.unscaledTime;
            Turn(_page + (eventData.scrollDelta.y < 0f ? 1 : -1));
        }

        private void Turn(int page)
        {
            page = Mathf.Clamp(page, 0, Pages - 1);
            if (page != _page)
            {
                _page = page;
                Refresh();
            }
        }

        public void Refresh()
        {
            if (_working || !_ready || this == null)
            {
                return;
            }
            _working = true;
            try
            {
                if (!_aside && SomeoneElse())
                {
                    StepAside();
                }
                Collect();
                if (!_aside)
                {
                    Apply();
                }
            }
            catch (Exception e)
            {
                Logger?.Error(e);
                StepAside();
            }
            finally
            {
                _working = false;
            }
        }

        // The apps are the buttons of the game's kind in the grid that are showing, or that this pager has hidden.
        // A button somebody else keeps switched off is not counted: it is theirs to show.
        private void Collect()
        {
            _apps.Clear();
            for (int i = _hidden.Count - 1; i >= 0; i--)
            {
                if (_hidden[i] == null || _hidden[i].parent != transform)
                {
                    _hidden.RemoveAt(i);
                }
            }
            for (int i = 0; i < transform.childCount; i++)
            {
                Transform child = transform.GetChild(i);
                if (child == _template || child.GetComponent<AppButton>() == null)
                {
                    continue;
                }
                if (child.gameObject.activeSelf || _hidden.Contains(child))
                {
                    _apps.Add(child);
                }
            }
            Pages = Mathf.Max(1, (_apps.Count + PerPage - 1) / PerPage);
            _page = Mathf.Clamp(_page, 0, Pages - 1);
        }

        private bool SomeoneElse()
        {
            for (int i = 0; i < _hidden.Count; i++)
            {
                if (_hidden[i] != null && _hidden[i].gameObject.activeSelf)
                {
                    return true;   // a button this pager switched off has been switched on by another hand
                }
            }
            _scripts.Clear();
            GetComponents(_scripts);
            for (int i = 0; i < _scripts.Count; i++)
            {
                MonoBehaviour script = _scripts[i];
                if (script == null || script == this)
                {
                    continue;
                }
                string assembly = script.GetType().Assembly.GetName().Name;
                bool known = script.GetType().Assembly == typeof(SmartphoneUI).Assembly || assembly.StartsWith("Unity", StringComparison.Ordinal)
                             || assembly.StartsWith("BigAmbitions", StringComparison.Ordinal) || assembly == "HGPlugins" || assembly == "HGExtensions"
                             || assembly == "ExternalPlugins" || script.GetType().Assembly == typeof(PhoneGridPager).Assembly;
                if (!known)
                {
                    return true;
                }
            }
            return false;
        }

        private void StepAside()
        {
            _aside = true;
            for (int i = 0; i < _hidden.Count; i++)
            {
                if (_hidden[i] != null)
                {
                    _hidden[i].gameObject.SetActive(true);
                }
            }
            _hidden.Clear();
            if (_dots != null)
            {
                _dots.gameObject.SetActive(false);
            }
            UiClone.Fallback("another mod pages the phone's home screen, so this one leaves the pages to it");
        }

        private void Apply()
        {
            for (int i = 0; i < _apps.Count; i++)
            {
                Transform app = _apps[i];
                bool show = i / PerPage == _page;
                if (app.gameObject.activeSelf != show)
                {
                    app.gameObject.SetActive(show);
                }
                bool hidden = _hidden.Contains(app);
                if (!show && !hidden)
                {
                    _hidden.Add(app);
                }
                else if (show && hidden)
                {
                    _hidden.Remove(app);
                }
            }
            Dots();
        }

        // The page dots stand under the grid, on the phone's own panel (not in the grid: its layout would place them).
        private void Dots()
        {
            if (Pages < 2)
            {
                if (_dots != null)
                {
                    _dots.gameObject.SetActive(false);
                }
                return;
            }
            if (_dots == null)
            {
                var holder = new GameObject("RealLife PageDots", typeof(RectTransform));
                _dots = (RectTransform)holder.transform;
                _dots.SetParent(transform.parent, false);
                _dots.anchorMin = _dots.anchorMax = new Vector2(0.5f, 0f);
                _dots.pivot = new Vector2(0.5f, 0.5f);
                _dots.anchoredPosition = new Vector2(0f, DotsFromBottom);
                _dots.sizeDelta = new Vector2(200f, DotSize + 16f);
            }
            _dots.gameObject.SetActive(true);
            while (_dotImages.Count < Pages)
            {
                int page = _dotImages.Count;
                var dot = new GameObject("Dot", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
                dot.transform.SetParent(_dots, false);
                var image = dot.GetComponent<Image>();
                image.sprite = _dotSprite;
                var button = dot.GetComponent<Button>();
                button.transition = Selectable.Transition.None;
                button.onClick.AddListener(() => Turn(page));
                _dotImages.Add(image);
            }
            float width = Pages * DotSize + (Pages - 1) * DotGap;
            for (int i = 0; i < _dotImages.Count; i++)
            {
                Image image = _dotImages[i];
                image.gameObject.SetActive(i < Pages);
                var rect = (RectTransform)image.transform;
                rect.sizeDelta = new Vector2(DotSize, DotSize);
                rect.anchoredPosition = new Vector2(-width * 0.5f + DotSize * 0.5f + i * (DotSize + DotGap), 0f);
            }
            Paint();
        }

        private void Paint()
        {
            if (_dots == null || _aside)
            {
                return;
            }
            var references = InstanceBehavior<GlobalReferences>.Instance;
            Color red = references != null ? (Color)references.colors.red : Color.red;
            for (int i = 0; i < _dotImages.Count && i < Pages; i++)
            {
                bool wanted = false;
                for (int a = 0; a < _attention.Count; a++)
                {
                    int index = _apps.IndexOf(_attention[a]);
                    wanted |= index >= 0 && index / PerPage == i;
                }
                _dotImages[i].color = i == _page ? Color.white : wanted ? red : new Color(1f, 1f, 1f, 0.35f);
            }
        }

        private void OnDestroy()
        {
            for (int i = 0; i < _hidden.Count; i++)
            {
                if (_hidden[i] != null)
                {
                    _hidden[i].gameObject.SetActive(true);
                }
            }
            _hidden.Clear();
            if (_dots != null)
            {
                Destroy(_dots.gameObject);
            }
        }
    }
}
