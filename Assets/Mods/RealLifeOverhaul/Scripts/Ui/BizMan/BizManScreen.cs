using System;
using System.Collections.Generic;
using TMPro;
using UI.Smartphone.Apps.BizMan;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// BizMan as a screen of the kit, mirrored from the game's own app: the portfolio from its list (the businesses as the
    /// game sorts and filters them, the homes, the warehouses, the headquarters), and a place's page from its business view
    /// (its tab bar's buttons as the sidebar, another mod's among them, the head, and the shown tab's page). Every press goes
    /// to the game's own button or the public method a button calls, so the game's questions, refusals and other mods'
    /// patches all run; nothing of the game's is worked out here. A page only the game's own view shows hands over to it.
    /// Cost: a look at a few counts and flags four times a second while it shows; a layout only when they change.
    /// </summary>
    public sealed partial class BizManScreen : IUiScreen, IUiTyping, IUiStepping, IUiPointing
    {
        private const float Every = 0.25f;   // seconds between looks at the game's BizMan

        private static readonly List<Button> Buttons = new List<Button>(64);
        private static readonly Dictionary<int, Sprite> Pictures = new Dictionary<int, Sprite>(32);
        private static bool _picturesOn;

        private readonly BizMan _app;
        private readonly BizManModel _m = new BizManModel();
        private bool _dirty = true;
        private float _next;
        private long _seen;
        private int _typeCommand = BizManPages.Type, _typeArg = BizManPages.FieldName;

        public BizManScreen(BizMan app)
        {
            _app = app;
            if (!_picturesOn)
            {
                // The game's own pictures (logos, the building's photo, the kinds of business) by the keys this screen gives them;
                // any other key goes to whoever asked before.
                _picturesOn = true;
                Func<string, Sprite> before = UiPictures.Find;
                UiPictures.Find = key => key != null && key.StartsWith(PictureKey, StringComparison.Ordinal) ? Picture(key) : before?.Invoke(key);
            }
        }

        public string Id => "bizman";

        public bool Dirty
        {
            get
            {
                if (Time.unscaledTime >= _next)
                {
                    _next = Time.unscaledTime + Every;
                    long now = Signature();
                    _dirty |= now != _seen;
                    _seen = now;
                }
                return _dirty;
            }
        }

        public void Lay(UiScene s, UiFrame frame, bool dark, IUiKeys keys)
        {
            Build();
            BizManPages.Lay(s, _m, frame, dark, keys);
            Clips(s);
            _dirty = false;
        }

        // ------------------------------------------------------------------------------------------------- what changed

        // What changing would change the screen: which face shows, the place and its tab, the list's rows and filters, and
        // the shown page's own few counts and flags.
        private long Signature()
        {
            BizManBusiness b = _app.business;
            bool page = b != null && b.gameObject.activeSelf;
            long sig = page ? 1 : 2;
            if (!page)
            {
                return sig * 31 + PortfolioSignature();
            }
            sig = sig * 31 + (b.address != null ? b.address.GetHashCode() : 0);
            sig = sig * 31 + Hash(GameContract.BizTab.Field?.GetValue(b) as string);
            sig = sig * 31 + Hash(_shown);
            Transform menu = GameContract.BizMenu.Field?.GetValue(b) as Transform;
            for (int i = 0; menu != null && i < menu.childCount; i++)
            {
                sig = sig * 3 + (menu.GetChild(i).gameObject.activeSelf ? 1 : 0);
            }
            return sig * 31 + PageSignature(b);
        }

        private static int Hash(string text) => text != null ? text.GetHashCode() : 0;

        // ------------------------------------------------------------------------------------------------- the model

        private void Build()
        {
            Pictures.Clear();
            _m.Title = UiText.T("smartphone_bizman");
            BizManBusiness b = _app.business;
            if (b != null && b.gameObject.activeSelf && b.buildingRegistration != null)
            {
                BuildPage(b);
            }
            else
            {
                BuildPortfolio();
            }
            var prompts = new List<UiPrompt>(5)
            {
                new UiPrompt { Action = UiAction.Move, Label = UiText.T("reallife_ui_prompt_move") },
                new UiPrompt { Action = UiAction.Confirm, Label = UiText.T("reallife_ui_prompt_select") },
                new UiPrompt { Action = UiAction.TabPrev, Label = UiText.T(_m.Page == BizManPage.Portfolio ? "reallife_ui_prompt_group" : "reallife_ui_bizman_prompt_section") },
                new UiPrompt { Action = UiAction.Back, Label = UiText.T("reallife_ui_prompt_back") },
            };
            _m.Prompts = prompts.ToArray();
            _m.PromptCount = _m.Prompts.Length;
        }

        // ------------------------------------------------------------------------------------------------- presses

        public bool Press(int command, int arg)
        {
            _dirty = true;
            return _m.Page == BizManPage.Portfolio ? PressPortfolio(command, arg) : PressPage(command, arg);
        }

        public void Focused(int command, int arg)
        {
            Aim(command, arg);
            // A tab is chosen as the pad comes to it, so the ring is never on one tab while another is chosen.
            if ((command == BizManPages.Period && arg != _m.Period) || (command == BizManPages.Page && arg != _m.PageShown))
            {
                Press(command, arg);
            }
            if (command == UiChart.First + UiChart.Point && _m.Chart != null && _m.Chart.Focus != arg)
            {
                _m.Chart.Focus = arg;
                _dirty = true;
            }
        }

        public void Pointed(int command, int arg)
        {
            Aim(command, arg);
            if (command == UiChart.First + UiChart.Point && _m.Chart != null && _m.Chart.Focus != arg)
            {
                _m.Chart.Focus = arg;   // the mouse resting on a point reads it in its card
                _dirty = true;
            }
        }

        // The field a press would type into: the last one the keys or the mouse came to.
        private void Aim(int command, int arg)
        {
            if (command == BizManPages.Type || command == UiDataTable.First + UiDataTable.Search || command == BizManPages.Goods + UiDataTable.Type || command == BizManPages.OpsType
                || command == BizManPages.OpsSearch || command == BizManPages.People + UiDataTable.Search || command == BizManPages.Table + UiDataTable.Type)
            {
                _typeCommand = command;
                _typeArg = arg;
            }
        }

        public bool Back()
        {
            _dirty = true;
            if (CloseLists() || OpsBack() || CardBack() || (_m.Part == BizManPart.Schedule && ScheduleBack()))
            {
                return true;
            }
            if (_m.Page != BizManPage.Business)
            {
                return false;
            }
            BackToList();
            return true;
        }

        public void Tab(int step)
        {
            int count = _m.Nav.Length;
            if (count < 2)
            {
                return;
            }
            int next = ((_m.NavSelected + step) % count + count) % count;
            Press(BizManPages.Section, next);
        }

        public bool Act(UiAction action)
        {
            if (Pressing(_m.Head?.Actions, action, UiFirmHead.First + UiFirmHead.Action, true) || Pressing(_m.Actions, action, BizManPages.Action, false))
            {
                return true;
            }
            if (_m.Amount != null && _m.Amount.Send.Key == action && !_m.Amount.Send.Disabled)
            {
                return Press(BizManPages.Action, _m.Amount.Send.Arg);
            }
            if (_m.Catalogue != null && _m.Catalogue.Actions.Length > 0 && _m.Catalogue.Actions[0].Key == action)
            {
                return Press(UiCatalogue.First + UiCatalogue.Action, 0);
            }
            if (_m.Page == BizManPage.Business && (Pressing(_m.Ops.Actions, action, BizManPages.OpsAction, false) || Pressing(_m.Ops.PlanActions, action, BizManPages.OpsAction, false)))
            {
                return true;
            }
            return false;
        }

        // The action a key runs from anywhere, pressed as its button would be: by its index in the head, by what it does on a page.
        private bool Pressing(UiActionModel[] actions, UiAction key, int command, bool byIndex)
        {
            for (int i = 0; actions != null && i < actions.Length; i++)
            {
                if (actions[i].Key == key && !actions[i].Disabled)
                {
                    return Press(command, byIndex ? i : actions[i].Arg);
                }
            }
            return false;
        }

        public void Reveal(int list, int index)
        {
            if (_m.Part == BizManPart.Interior && _m.Page == BizManPage.Business && (list == _m.LinesList || list == _m.SecondList))
            {
                if (list == _m.LinesList)
                {
                    _m.LinesReveal = index;
                }
                else
                {
                    _m.SecondReveal = index;
                }
                _dirty = true;
                return;
            }
            if (OpsReveal(list, index))
            {
                _dirty = true;
                return;
            }
            UiDataTableModel table = _m.Page == BizManPage.Portfolio ? _m.Table : _m.Goods;
            if (table != null)
            {
                table.Reveal = index;
            }
            if (_m.Catalogue != null && _m.Page == BizManPage.Business && _m.Part == BizManPart.Start)
            {
                _m.Catalogue.Reveal = index;
            }
            _dirty = true;
        }

        public void Wheel(int clip, float notches)
        {
            float by = -notches * UiTokens.S16;
            if (OpsWheel(clip, by))
            {
                _dirty = true;
                return;
            }
            if (_m.Part == BizManPart.Interior && _m.Page == BizManPage.Business && (clip == _m.LinesClip || clip == _m.SecondClip))
            {
                if (clip == _m.LinesClip)
                {
                    _m.LinesOffset = Math.Max(0f, _m.LinesOffset + by);
                }
                else
                {
                    _m.SecondOffset = Math.Max(0f, _m.SecondOffset + by);
                }
                _dirty = true;
                return;
            }
            UiDataTableModel table = _m.Page == BizManPage.Portfolio ? _m.Table : _m.Goods;
            if (table != null)
            {
                table.Offset = Math.Max(0f, table.Offset + by);
            }
            if (_m.Catalogue != null)
            {
                _m.Catalogue.Offset = Math.Max(0f, _m.Catalogue.Offset + by);
            }
            _dirty = true;
        }

        // ------------------------------------------------------------------------------------------------- typing

        public int TypingCommand => _typeCommand;

        public string TypingText => OpsTyping(_typeCommand, out string ops) ? ops : Field(_typeCommand, _typeArg) is TMP_InputField f ? f.text : null;


        public void Typed(string text)
        {
            if (OpsTyped(_typeCommand, _typeArg, text))
            {
                _dirty = true;
                return;
            }
            if (Field(_typeCommand, _typeArg) is TMP_InputField f)
            {
                UiWidgets.Type(f, text, false);   // its change listeners run as typing into it would run them
                if (_typeCommand == UiDataTable.First + UiDataTable.Search)
                {
                    ShopsTable?.RefreshFilters();   // the list filtered again even while its filter panel, which hears its field, is closed
                }
                _dirty = true;
            }
        }


        // ------------------------------------------------------------------------------------------------- stepping

        public bool Steps(int command) => (command == BizManPages.Slider && _sliderHeld) || (command == BizManPages.OpsSlider && _opsHeld >= 0);

        public void Step(int command, int arg, int step)
        {
            if (command == BizManPages.Slider)
            {
                StepRent(step);
            }
            else if (command == BizManPages.OpsSlider && arg == _opsHeld)
            {
                StepSlider(arg, step);
            }
        }

        // ------------------------------------------------------------------------------------------------- the game's pieces

        private const string PictureKey = "bizman:";

        // A sprite of the game's as a picture key the renderer asks for again only when the sprite itself changes.
        private static string Pic(Sprite sprite)
        {
            if (sprite == null)
            {
                return null;
            }
            int id = sprite.GetInstanceID();
            Pictures[id] = sprite;
            return PictureKey + id.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        private static float Aspect(Sprite sprite) => sprite != null && sprite.rect.height > 0f ? sprite.rect.width / sprite.rect.height : 1f;

        private static Sprite Picture(string key)
        {
            return int.TryParse(key.Substring(PictureKey.Length), System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out int id)
                   && Pictures.TryGetValue(id, out Sprite sprite) ? sprite : null;
        }

        // A field of the game's read from its contract line; null when this version lacks it.
        private static T Get<T>(GameMember member, object owner) where T : class => owner != null && member.Field != null ? member.Field.GetValue(owner) as T : null;

        // The words a piece shows, its rich text taken off; null when it is not shown.
        private static string Words(Component c) => c != null && c.gameObject.activeInHierarchy ? UiWidgets.Label(c) : null;

        // The words at a path under a root, as the game's own GetLabelByName finds them.
        private static string WordsAt(Transform root, string path)
        {
            Transform t = root != null ? root.Find(path) : null;
            return t != null ? Words(t) : null;
        }

        // The button under a root whose click the game wired to a method of that name and that shows now; null when none does.
        private static Button Calling(Component root, string method)
        {
            Buttons.Clear();
            if (root == null)
            {
                return null;
            }
            root.GetComponentsInChildren(true, Buttons);
            Button found = null;
            for (int i = 0; i < Buttons.Count && found == null; i++)
            {
                UnityEvent click = Buttons[i].onClick;
                int n = click.GetPersistentEventCount();
                for (int k = 0; k < n; k++)
                {
                    if (click.GetPersistentMethodName(k) == method && Buttons[i].gameObject.activeInHierarchy)
                    {
                        found = Buttons[i];
                        break;
                    }
                }
            }
            Buttons.Clear();
            return found;
        }

        // A button pressed as a click presses it, with one line in Player.log so a report shows each press reaching the game.
        private static bool Click(Button b, string what)
        {
            bool done = UiWidgets.Press(b);
            UiDirector.Logger?.Info("BizMan (kit): " + what + (done ? ": handed to the game" : " was not shown by the game, so nothing was pressed"));
            return done;
        }

        // A role for the colour the game paints a word in: its green good, its red bad, its yellow or orange a warning.
        private static UiRole Tone(Color c)
        {
            if (c.a <= 0.01f)
            {
                return UiRole.Muted;
            }
            if (c.g > 0.5f && c.r < 0.5f)
            {
                return UiRole.Ok;
            }
            if (c.r > 0.6f && c.g < 0.35f)
            {
                return UiRole.Danger;
            }
            if (c.r > 0.6f && c.g > 0.4f && c.b < 0.4f)
            {
                return UiRole.Warn;
            }
            return UiRole.Muted;
        }

        private static UiRole Ink(UiRole tone) => tone == UiRole.Ok ? UiRole.OkText : tone == UiRole.Danger ? UiRole.DangerText : tone == UiRole.Warn ? UiRole.WarnText : UiRole.None;
    }
}
