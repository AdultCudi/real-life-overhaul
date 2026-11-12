using System;
using System.Collections.Generic;
using Player.HUD.ItemInfoOverlays;
using UnityEngine;
using UnityEngine.UI;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The cards over the world's items on the kit: whatever the game's OverlayManager shows over a shelf, a machine, a till, a
    /// fridge or a person (its small card under the pointer, its large card after a click) drawn as the kit's overlay card
    /// (<see cref="UiOverlayCard"/>) where the game put its own box, and a person's large card as the info panel docked at the
    /// top left. The game's OverlayManager still decides what shows and where: the cards read what its overlays set after the
    /// game and other mods wrote it (Going Public's patch on the stock overlay, the mod's Cook and Family buttons in the
    /// fridge), the game's boxes stay alive and hidden by leaf, every button is the game's own button pressed as a click presses
    /// it, and every choice goes through the game's own drop-down. The card follows the game's own projected point, so a shown
    /// card costs no projection of its own beyond its leader. Read again when the game shows another thing, when a card is
    /// pressed, and four times a second while it shows; laid out again only when what it says changed. Cost a frame: a few
    /// comparisons and one move; nothing allocated, searched or queried.
    /// </summary>
    internal sealed partial class OverlayCardsLive
    {
        private const float ReadEvery = 0.25f;   // s of real time between reads of the game's box while a card shows

        private enum Form : byte { None, Hover, Click }

        private readonly UiScene _scene = new UiScene();
        private readonly UiScene _leaderScene = new UiScene();
        private readonly UiConceal _simpleHide = new UiConceal();
        private readonly UiConceal _detailHide = new UiConceal();
        private readonly UiOverlayModel _model = new UiOverlayModel();
        private readonly Button[] _buttons = new Button[UiOverlayModel.MostActions];
        private readonly Action[] _does = new Action[UiOverlayModel.MostActions];
        private UiView _view, _leader;
        private CanvasGroup _group;
        private float _scale = UiTokens.CanvasPerPx;
        private OverlayManager _manager;
        private SimpleOverlay _simple;
        private DetailedOverlay _detailed;
        private Transform _anchor;   // the large box's first button as the game placed it: where the click was
        private Form _form;
        private int _seenId, _digest = int.MinValue;
        private float _due;
        private bool _off, _relay;
        private Vector2 _at = new Vector2(float.NaN, float.NaN);
        private string _state = "not looked at yet";

        /// <summary>What the cards are doing, for the interface's line at load.</summary>
        public string State => _off ? _state : _form == Form.Hover ? "the small card drawn over the game's box" : _form == Form.Click ? "the large card drawn over the game's box" : _state;

        /// <summary>The model last read, for the tests.</summary>
        public UiOverlayModel Model => _model;

        /// <summary>True while the small card stands in the game's box's place.</summary>
        public bool HoverShowing => _form == Form.Hover;

        /// <summary>True while the large card or the person's panel stands in the game's box's place.</summary>
        public bool ClickShowing => _form == Form.Click || EmployeeShowing;

        public void Build(RectTransform rect, float scale)
        {
            _scale = scale;
            _view = new UiView(rect, "Overlay") { Scale = scale, Ring = false };
            _view.PointerPressed = Guard.Wrap<int>(Pressed);
            _view.PointerEntered = Guard.Wrap<int>(node => _view.SetHover(node));
            _view.PointerExited = Guard.Wrap<int>(node => _view.SetHover(-1));
            _group = _view.Root.gameObject.AddComponent<CanvasGroup>();
            _view.Root.gameObject.SetActive(false);
            _leader = new UiView(rect, "OverlayLeader") { Scale = scale, Ring = false };
            UiOverlayCard.LayLeader(_leaderScene);
            _leader.Write(_leaderScene, false);
            _leader.Root.gameObject.SetActive(false);
            BuildEmployee(rect, scale);
        }

        public void Rescale(float scale)
        {
            _scale = scale;
            if (_view != null)
            {
                _view.Scale = scale;
                _leader.Scale = scale;
            }
            RescaleEmployee(scale);
            _digest = int.MinValue;   // laid out again at the new scale
            _at = new Vector2(float.NaN, float.NaN);
        }

        /// <summary>A frame: the card in the game's box's place while the game shows a box and its switch is on, the person's panel docked.</summary>
        public void Frame(UiFrame frame, bool hoverOn, bool clickOn, bool personOn)
        {
            if (_off || !Find())
            {
                return;
            }
            bool click = _detailed.isActiveAndEnabled && _detailed.relevantController != null;
            bool hover = !click && _simple.isActiveAndEnabled && _simple.relevantController != null;
            // A person's box needs the person's panel: with its switch off the whole box is the game's own.
            bool person = click && _person != null && _person.gameObject.activeSelf && !personOn;
            Form want = click && clickOn && !person ? Form.Click : hover && hoverOn ? Form.Hover : Form.None;
            if (want != _form)
            {
                Leave();
                _form = want;
                _seenId = 0;
                _due = 0f;
                _digest = int.MinValue;
                _openChoice = -1;
                _page = 0;
            }
            if (_form == Form.None)
            {
                EmployeeFrame(frame, false);
                return;
            }
            EntityController entity = _form == Form.Hover ? _simple.relevantController : _detailed.relevantController;
            int id = entity.GetInstanceID();
            float now = Time.unscaledTime;
            if (id != _seenId)
            {
                _seenId = id;
                _openChoice = -1;
                _page = 0;
                _due = 0f;
            }
            bool renaming = _form == Form.Click && Typing();
            if (now >= _due || _relay || renaming)
            {
                _due = now + ReadEvery;
                _relay = false;
                if (!Read(entity, renaming))
                {
                    Leave();
                    return;
                }
            }
            bool panel = EmployeeFrame(frame, _form == Form.Click);
            bool card = _model.Title != null || _model.RowCount > 0 || _model.CheckCount > 0 || _model.ActionCount > 0 || !string.IsNullOrEmpty(_model.Figure);
            if (!card && !panel)
            {
                Leave();   // nothing for a card to say: the game's box says nothing either
                return;
            }
            Conceal();
            if (!card)
            {
                _view.Root.gameObject.SetActive(false);
                _leader.Root.gameObject.SetActive(false);
                return;
            }
            if (!_view.Root.gameObject.activeSelf)
            {
                _group.blocksRaycasts = _form == Form.Click;   // the small card is only to read: the pointer passes through it to the world
                _view.Root.gameObject.SetActive(true);
            }
            Place(frame);
        }

        /// <summary>The game's boxes back as they were, the cards gone.</summary>
        public void Give()
        {
            Leave();
            _simpleHide.Clear();
            _detailHide.Clear();
            GiveEmployee();
        }

        // The card put away, the box it stood over shown again.
        private void Leave()
        {
            if (_view != null)
            {
                _view.Root.gameObject.SetActive(false);
                _leader.Root.gameObject.SetActive(false);
            }
            _simpleHide.Show();
            _detailHide.Show();
            _form = Form.None;
            _model.Clear();
            _digest = int.MinValue;
            EmployeeLeave();
        }

        // The game's box under what is drawn over it, hidden by leaf and given back as it was found.
        private void Conceal()
        {
            if (_form == Form.Hover)
            {
                _simpleHide.Hide();
            }
            else
            {
                _detailHide.Hide();
            }
        }

        // The game's manager and its two boxes, found again with each city's interface.
        private bool Find()
        {
            if (_manager != null)
            {
                return true;
            }
            _manager = InstanceBehavior<OverlayManager>.Instance;
            if (_manager == null)
            {
                return false;
            }
            _simple = GameContract.OverlaySimple.Field?.GetValue(_manager) as SimpleOverlay;
            _detailed = GameContract.OverlayDetailed.Field?.GetValue(_manager) as DetailedOverlay;
            if (_simple == null || _detailed == null || !FindHover() || !FindClick())
            {
                _off = true;
                _state = "the game's own (its boxes are not built as this version expects)";
                GameUi.UiClone.Fallback("the game's cards over an item are not built as expected, so they stay the game's own");
                _manager = null;
                return false;
            }
            _simpleHide.Add(_simple.transform);
            _detailHide.Add(_detailed.transform);
            _state = "waiting for a box";
            return true;
        }

        // The small card beside the game's own projected point, clamped above the status bar. The large card where the game's box puts
        // its first button, which is where the player clicked: the card's first target under that point, as the game's own box stands.
        private void Place(UiFrame frame)
        {
            float w = UiOverlayCard.Width(_model), h = UiOverlayCard.Height(_model);
            Vector2 p;
            float x, y;
            if (_form == Form.Click && _scene.NodeCount > 0)
            {
                p = WorldHost.ToLayout(_anchor != null ? _anchor.position : _detailed.transform.position, frame);
                ref UiNode first = ref _scene.Nodes[0];
                x = p.x - (first.X + first.W / 2f);
                y = p.y - (first.Y + first.H / 2f);
                UiHudRules.Clamp(ref x, ref y, w, h, frame.Width, frame.BandY);
            }
            else
            {
                p = WorldHost.ToLayout((_form == Form.Hover ? _simple.transform : _detailed.transform).position, frame);
                UiHudRules.Beside(p.x, p.y, true, w, h, frame.Width, frame.BandY, 0f, 0f, 0f, 0f, out x, out y);
            }
            x = Mathf.Round(x);
            y = Mathf.Round(y);
            var at = new Vector2(x * _scale, -y * _scale);
            if (at != _at)
            {
                _at = at;
                _view.Root.anchoredPosition = at;
                bool leader = _form == Form.Hover && OverlayRules.LeaderShows(p.x, p.y, x, y, w, h, UiHudRules.Lift);
                if (leader)
                {
                    _leader.Root.anchoredPosition = new Vector2((p.x - UiOverlayCard.LeaderWidth / 2f) * _scale, -(p.y - UiHudRules.Lift) * _scale);
                }
                _leader.Root.gameObject.SetActive(leader);
            }
        }

        // The model laid out, when what it says changed.
        private void Lay()
        {
            int digest = _model.Digest();
            if (digest == _digest)
            {
                return;
            }
            _digest = digest;
            float w = UiOverlayCard.Width(_model), h = UiOverlayCard.Height(_model);
            _scene.Begin(_form == Form.Hover ? UiOverlays.HoverId : UiOverlays.ClickId, w, h, true);
            _scene.Floats = true;
            _scene.Pointer = true;
            UiOverlayCard.Lay(_scene, UiAt.Screen, _model, 0f, 0f);
            _view.Write(_scene, false);
            _at = new Vector2(float.NaN, float.NaN);
        }

        // A press on the card: the game's own button, or the choice, page or volume the game's own widget takes.
        private void Pressed(int node)
        {
            if (node < 0 || node >= _scene.NodeCount)
            {
                return;
            }
            ref UiNode n = ref _scene.Nodes[node];
            bool done = !n.Disabled && Press(n.Command, n.Arg);
            if (done)
            {
                _view.Press(Time.unscaledTime);
            }
            UiGame.Instance.Play(done ? UiCue.Confirm : UiCue.Error);
        }

        private bool Press(int command, int arg)
        {
            if (_form != Form.Click)
            {
                return false;
            }
            switch (OverlayRules.Route(_model, command, arg, _openChoice))
            {
                case OverlayAct.Action:
                    bool pressed = _does[arg] != null ? Run(_does[arg]) : UiWidgets.Press(_buttons[arg]);
                    _due = Time.unscaledTime + 0.05f;
                    return pressed;
                case OverlayAct.Open:
                    _openChoice = _openChoice == arg ? -1 : arg;
                    _page = _openChoice >= 0 ? OverlayRules.PageOf(_model.Choices[arg].Chosen, UiOverlayModel.PerPage) : 0;
                    _relay = true;
                    return true;
                case OverlayAct.Choose:
                    bool chosen = Choose(arg);
                    _openChoice = -1;
                    _due = Time.unscaledTime + 0.05f;
                    return chosen;
                case OverlayAct.Page:
                    _page = OverlayRules.ClampPage(_page + arg, _model.Choices[_openChoice].OptionCount, UiOverlayModel.PerPage);
                    _relay = true;
                    return true;
                case OverlayAct.Step:
                    return Step(arg);
            }
            return false;
        }

        // A game handler run as the game's own work (its listeners and other mods' patches are not the mod's cost).
        private static bool Run(Action act)
        {
            using (HotSpots.Game())
            {
                act();
            }
            return true;
        }
    }
}
