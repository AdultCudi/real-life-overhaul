using System;
using Entities;
using Tooltip;
using UnityEngine;
using UnityEngine.UI;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The tooltip card in the city: a canvas of the kit's own nested in the game's tooltip canvas (UiOverCanvas), so the game's scaler and
    /// UI zoom scale it as they scale the game's tooltip, and one order above it. While the game shows a tooltip (TooltipSystem.IsVisible)
    /// the card draws what the game's builder was given (TooltipRead) in the kit's look, placed as the game places its own (UiTooltipRules.Place)
    /// and following the mouse as the game's does; the game's tooltip stays alive and its content is only hidden under a fade group of
    /// ours (UiConceal), so other mods that read it still do. A tooltip the card cannot draw (a picture) is left to the game's own. The
    /// net worth card, asked for by the money pill, stands over the pill. The card takes no pointer and no key. Cost a frame: one static
    /// read; while a tooltip shows, two reads of its parent and the mouse's place; a layout only when the words change; nothing allocated.
    /// </summary>
    internal sealed class TooltipHost : MonoBehaviour
    {
        private static readonly FeatureGuard FrameGuard = Guard.For("ui", "TooltipHost", typeof(TooltipHost));
        private static readonly int HotUpdate = HotSpots.Slot("Ui: TooltipHost.Update");
        private const float MoneyDelay = 0.1f;   // seconds the pointer rests on the money before its card shows, as the game waits for its own tooltips

        private readonly UiScene _scene = new UiScene();
        private readonly UiTooltipModel _model = new UiTooltipModel();
        private TooltipRead _read;
        private readonly UiConceal _conceal = new UiConceal();
        private UiView _view;
        private RectTransform _rect;
        private Vector2 _laid;
        private Vector2 _mouse = new Vector2(float.NaN, float.NaN);
        private float _scale = UiTokens.CanvasPerPx, _w, _h;
        private int _sig;
        private bool _readDue, _shown, _gameOwn, _kind;   // _kind: the card shown is the money's
        private bool _moneyAsked;
        private float _moneyDue, _moneyX, _moneyY, _moneyW;   // the pill's box as fractions of the screen
        private bool _faulted, _byScale, _scaleHeld;
        private Transform _data;
        private Vector3 _scaleWas = Vector3.one;

        /// <summary>The card is on screen now (the game's tooltip or the money's), for the tests.</summary>
        internal bool Showing => _shown;

        /// <summary>The words last laid out, for the tests.</summary>
        internal UiTooltipModel Model => _model;

        /// <summary>The scene last laid out, for the tests.</summary>
        internal UiScene Scene => _scene;

        /// <summary>
        /// Builds the host on the game's tooltip system; a reason in words when it is not built as this version expects, else null. The game's
        /// content is hidden at once and given back by <see cref="Clear"/>.
        /// </summary>
        internal static string Make(TooltipSystem system, out TooltipHost host)
        {
            host = null;
            Canvas canvas = GameContract.TipCanvas.Field != null ? GameContract.TipCanvas.Field.GetValue(system) as Canvas : null;
            Transform data = GameContract.TipData.Field != null ? GameContract.TipData.Field.GetValue(system) as Transform : null;
            if (canvas == null || data == null)
            {
                return "its canvas or its content is not there";
            }
            var read = new TooltipRead();
            string why = read.Bind(system);
            if (why != null)
            {
                return why;
            }
            RectTransform rect = UiOverCanvas.Make(canvas, "RealLife Interface Tooltip");
            if (rect == null)
            {
                return "no canvas stands above it";
            }
            var raycaster = rect.GetComponent<GraphicRaycaster>();
            if (raycaster != null)
            {
                raycaster.enabled = false;   // the card takes no pointer: the target under it keeps the pointer, so its tooltip does not flicker
            }
            host = rect.gameObject.AddComponent<TooltipHost>();
            CanvasGroup gameGroup = GameContract.TipGroup.Field != null ? GameContract.TipGroup.Field.GetValue(system) as CanvasGroup : null;
            host.Build(rect, read, data, gameGroup != null && data.GetComponent<CanvasGroup>() == gameGroup);
            return null;
        }

        private void Build(RectTransform rect, TooltipRead read, Transform data, bool byScale)
        {
            _rect = rect;
            _read = read;
            _view = new UiView(rect, "Tooltip") { Scale = _scale, Ring = false };
            var group = _view.Root.gameObject.AddComponent<CanvasGroup>();
            group.interactable = false;
            group.blocksRaycasts = false;
            _view.Root.gameObject.SetActive(false);
            _data = data;
            _byScale = byScale;   // the game's own fade group stands on its content: that group is its to write, so the content is hidden by its scale instead
            if (!byScale)
            {
                _conceal.Add(data);   // the game's own words stay where they are, unseen and untouched, under a group of ours
            }
            rect.gameObject.SetActive(true);
            Conceal(true);
        }

        // The game's content unseen, or back exactly as found: a fade group of ours, or its scale where the game's own group stands on it.
        private void Conceal(bool on)
        {
            if (!_byScale)
            {
                if (on)
                {
                    _conceal.Hide();
                }
                else
                {
                    _conceal.Show();
                }
                return;
            }
            if (_data == null || _scaleHeld == on)
            {
                return;
            }
            _scaleHeld = on;
            if (on)
            {
                _scaleWas = _data.localScale;
                _data.localScale = Vector3.zero;
            }
            else
            {
                _data.localScale = _scaleWas;
            }
        }

        /// <summary>The game's tooltip back as it was, the card gone.</summary>
        internal void Clear()
        {
            Conceal(false);
            _conceal.Clear();
            if (_rect != null)
            {
                Destroy(_rect.gameObject);
            }
        }

        /// <summary>The pointer settled on the money pill (its box as fractions of the screen): the net worth card shows after a moment.</summary>
        internal void MoneyIn(float x, float y, float w)
        {
            _moneyAsked = true;
            _moneyDue = Time.unscaledTime + MoneyDelay;
            _moneyX = x;
            _moneyY = y;
            _moneyW = w;
            if (_shown && _kind)
            {
                HideCard();
            }
        }

        /// <summary>The pointer left the money pill, or pressed it: its card goes.</summary>
        internal void MoneyOut()
        {
            _moneyAsked = false;
            if (_shown && _kind)
            {
                HideCard();
            }
        }

        private void Update()
        {
            using var hotSpot = HotSpots.Time(HotUpdate);
            if (FrameGuard.Off || _faulted)
            {
                enabled = false;
                return;
            }
            try
            {
                OnUpdate();
            }
            catch (Exception e)
            {
                _faulted = true;
                Guard.Fail(FrameGuard, e, this);
                TooltipKit.Fault("a fault");
            }
        }

        private void OnUpdate()
        {
            if (!UiDirector.On || !UiScreenSwitches.On(TooltipKit.Id))
            {
                TooltipKit.Remove("its switch is off");
                return;
            }
            Vector2 size = _rect.rect.size;
            if (size != _laid)
            {
                Rescale(size);
            }
            if (_moneyAsked)
            {
                Money();
                return;
            }
            if (_shown && _kind)
            {
                HideCard();
            }
            if (!TooltipSystem.IsVisible)
            {
                if (_shown)
                {
                    HideCard();
                }
                if (_gameOwn)
                {
                    _gameOwn = false;
                    Conceal(true);   // the game's own tooltip is done: the next is read by the card again
                }
                _sig = 0;
                return;
            }
            int sig = _read.Signature();
            if (sig != _sig)
            {
                _sig = sig;
                _readDue = true;   // read a frame later: the game's old clones go at the end of this one
                return;
            }
            if (_readDue)
            {
                _readDue = false;
                ShowGame();
            }
            if (_shown)
            {
                Follow();
            }
        }

        // The canvas changed size (a resolution, the UI zoom): the px scale again, and the card laid out at it.
        private void Rescale(Vector2 size)
        {
            _laid = size;
            _scale = UiHudRules.Scale(size.x);
            _view.Scale = _scale;
            _sig = 0;
            if (_shown && _kind)
            {
                _moneyDue = 0f;   // the money's card laid out again at its place
                HideCard();
            }
        }

        // The game's tooltip drawn as the card, or left to the game when the card cannot draw it.
        private void ShowGame()
        {
            bool drawn = _read.Read(_model);
            if (drawn)
            {
                EmployeeInstance who = TooltipEmployee.Current();
                if (who != null)
                {
                    TooltipEmployee.Extend(_model, who);
                }
            }
            if (!drawn || _model.Empty)
            {
                if (!_gameOwn)
                {
                    _gameOwn = true;
                    Conceal(false);
                }
                HideCard();
                return;
            }
            if (_gameOwn)
            {
                _gameOwn = false;
                Conceal(true);
            }
            Lay();
            _kind = false;
            _mouse = new Vector2(float.NaN, float.NaN);
            _view.Root.gameObject.SetActive(true);
            _shown = true;
            Follow();
        }

        private void Lay()
        {
            _w = UiTooltipCard.Width(_model);
            _h = UiTooltipCard.Height(_model, _w);
            _scene.Begin("tooltip", _w, _h, true);
            _scene.Floats = true;
            UiTooltipCard.Lay(_scene, UiAt.Screen, _model, 0f, 0f, _w);
            _view.Write(_scene, false);
        }

        // The card follows the mouse as the game's tooltip does: placed again only when the mouse moved.
        private void Follow()
        {
            Vector3 m = Input.mousePosition;
            if (m.x == _mouse.x && m.y == _mouse.y)
            {
                return;
            }
            _mouse = new Vector2(m.x, m.y);
            float lw = _laid.x / _scale, lh = _laid.y / _scale;
            UiHudRules.ToLayout(m.x, m.y, Screen.width, Screen.height, lw, lh, out float px, out float py);
            UiTooltipRules.Place(px, py, _w, _h, lw, lh, UiTooltipRules.Gap * UiTokens.CanvasPerPx / _scale, out float x, out float y);
            _view.Root.anchoredPosition = new Vector2(x * _scale, -y * _scale);
        }

        private void HideCard()
        {
            if (!_shown)
            {
                return;
            }
            _shown = false;
            _view.Root.gameObject.SetActive(false);
        }

        // The net worth card: after the pointer has rested a moment on the money pill, made from today's figures and put over the pill.
        private void Money()
        {
            if (_shown || Time.unscaledTime < _moneyDue)
            {
                return;
            }
            if (!TooltipWorth.Make(_model))
            {
                _moneyAsked = false;
                return;
            }
            Lay();
            float lw = _laid.x / _scale, lh = _laid.y / _scale;
            UiTooltipRules.OverPill(_moneyX * lw, _moneyY * lh, _moneyW * lw, _w, _h, lw, UiTooltipRules.Gap, out float x, out float y);
            _view.Root.anchoredPosition = new Vector2(x * _scale, -y * _scale);
            _kind = true;
            _view.Root.gameObject.SetActive(true);
            _shown = true;
        }

        private void OnDestroy()
        {
            Conceal(false);
            _conceal.Clear();
        }
    }
}
