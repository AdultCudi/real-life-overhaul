using System;
using System.Collections.Generic;
using BigAmbitions.InteriorDesigner;
using BigAmbitions.InteriorDesigner.Tools;
using Buildings.Indoors.InteriorDesign;
using UI;
using UI.InteriorDesigner;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// Everything the interior designer's face reads from the game and presses in it, in one place. The game's designer (InteriorDesignerUI, its tools, its
    /// panels, its undo stack) stays alive and open under the kit with only its own layout unseen (UiConceal), so IsOpen stays true for Going Public, Immersion
    /// and VoogleRoute, and every press is the game's own: a tool through ToggleTool, undo, redo, the floors, the walls, apply and close through the methods its
    /// own buttons call, furniture through the placing its tiles call, a material through the buttons it made for it. The game says what changed through its
    /// own events (the balance, the cost, the score, the capacity); the face reads them, and everything else a few times a second from the game's own state.
    /// Cost: nothing while the designer is shut; while it shows, a comparison a frame and a read of a few dozen fields ten times a second, the lists rebuilt only
    /// when something changed.
    /// </summary>
    internal static partial class DesignerGame
    {
        /// <summary>Told when the game says something the face shows has changed (an event of its own), so the face reads it at once.</summary>
        public static Action Stale;

        // Tool by the order of the tool row; the row's index is the argument of a press.
        private static readonly ToolName[] Tools =
        {
            ToolName.Hand, ToolName.Furniture, ToolName.Duplicate, ToolName.Package, ToolName.Wall, ToolName.Floor, ToolName.Palette, ToolName.Eyedropper, ToolName.Producer,
            ToolName.Queue, ToolName.Security, ToolName.Sell, ToolName.TimeOfDay, ToolName.SaveBlueprint
        };

        private static InteriorDesignerUI _ui;
        private static ToolButton[] _buttons = new ToolButton[0];
        private static readonly List<ToolButton> Extras = new List<ToolButton>(2);   // toolbar buttons the kit's row has no tile for: another mod's tools
        private static bool _hooked, _opened;
        private static readonly UnityAction<bool> OnToggle = Guard.Listener<bool>(Toggled);
        private static readonly Action<double> OnBalance = Guard.Wrap<double>(Balance);
        private static readonly Action<double> OnCost = Guard.Wrap<double>(Cost);
        private static readonly Action<float> OnScore = Guard.Wrap<float>(Score);
        private static readonly Action OnCapacity = Guard.Wrap(Capacity);

        /// <summary>The game's designer, or null while its interface is not built.</summary>
        public static InteriorDesignerUI Ui
        {
            get
            {
                if (_ui == null)
                {
                    UIs uis = InstanceBehavior<UIs>.Instance;
                    _ui = uis != null ? uis.interiorDesignerUI : null;
                }
                return _ui;
            }
        }

        /// <summary>True while the game's designer is open in a city: the kit draws it and the game's rooms are the player's own.</summary>
        public static bool Open => InteriorDesignerUI.IsOpen && !InteriorDesignerHelper.BlueprintCreatorMode;

        /// <summary>The game's toolbar (it switches it on as the designer opens, off as it closes), which is what the kit's host watches.</summary>
        public static GameObject Toolbar()
        {
            InteriorDesignerUI ui = Ui;
            return ui != null && GameContract.DesignerToolbar.Field != null ? GameContract.DesignerToolbar.Field.GetValue(ui) as GameObject : null;
        }

        /// <summary>Why the designer is not built as this version expects, or null.</summary>
        public static string Check()
        {
            InteriorDesignerUI ui = Ui;
            if (ui == null)
            {
                return "the designer is not there";
            }
            if (Toolbar() == null)
            {
                return "its toolbar is not there";
            }
            if (GameContract.DesignerUndoStack.Field == null || GameContract.DesignerActionPanels.Field == null || GameContract.DesignerInfoPanels.Field == null)
            {
                return "its undo stack or its panels are not as this version has them";
            }
            return null;
        }

        private static UiConceal _conceal;

        /// <summary>
        /// The game's own pieces of the designer the kit hides, and nothing it must leave: its toolbar, its info panels, its action panels and the box of the blueprint.
        /// The game's colour picker and the world's overlays are not among them: the kit frames the one and leaves the others as they are.
        /// </summary>
        public static void Conceal(UiConceal conceal)
        {
            _conceal = conceal;
            InteriorDesignerUI ui = Ui;
            if (ui == null)
            {
                return;
            }
            GameObject toolbar = Toolbar();
            conceal.Add(toolbar != null ? toolbar.transform : null);   // another mod's tools get a tile in the kit's own row (Extras)
            ConcealPanels(conceal, Part(GameContract.DesignerInfoPanels, ui), GameContract.DesignerInfoList);
            ConcealPanels(conceal, Part(GameContract.DesignerActionPanels, ui), GameContract.DesignerPanelList);
            ConcealMore();
        }

        // A panel manager's own panels hidden one by one, so a panel another mod put in its list stays as the game shows it: the kit draws nothing for a tool it does not know,
        // and hiding the whole manager would leave that tool with no panel at all. Where the list cannot be read the whole manager is hidden, as before.
        private static void ConcealPanels(UiConceal conceal, Transform manager, GameMember list)
        {
            if (manager == null)
            {
                return;
            }
            Component owner = manager.GetComponent<MonoBehaviour>();
            var panels = list.Field != null && owner != null ? list.Field.GetValue(owner) as System.Collections.IEnumerable : null;
            if (panels == null)
            {
                conceal.Add(manager);
                return;
            }
            conceal.Shared(manager);
            string game = typeof(InteriorDesignerUI).Assembly.GetName().Name, own = typeof(DesignerGame).Assembly.GetName().Name;
            foreach (object panel in panels)
            {
                var part = panel as Component;
                if (part != null && !DesignerRules.Foreign(part.GetType().Assembly.GetName().Name, game, own))
                {
                    conceal.Add(part.transform);
                }
            }
        }

        // The box of the blueprint (it may stand outside the action panels) and the game's question box, as soon as the game has built them.
        private static void ConcealMore()
        {
            if (_conceal == null)
            {
                return;
            }
            DesignerPanes.Look(Ui);
            SaveBlueprintUI blueprint = BlueprintUi();
            _conceal.Add(blueprint != null ? blueprint.transform : null);
            DropdownSelectorUI box = Asker();
            var container = box != null ? BuyKits.Get<GameObject>(GameContract.DesignerAskContainer, box) : null;
            _conceal.Add(container != null ? container.transform : null);
        }

        private static Transform Part(GameMember member, object owner)
        {
            var behaviour = member.Field != null ? member.Field.GetValue(owner) as Component : null;
            return behaviour != null ? behaviour.transform : null;
        }

        // ------------------------------------------------------------------ opening and closing

        /// <summary>Puts the game's own events in hand while the interface is on: a new designer's opening and the readings it raises.</summary>
        public static void Hook()
        {
            if (_hooked)
            {
                return;
            }
            _hooked = true;
            PicturesOn();
            InteriorDesignerUI.OnInteriorDesignerToggle.AddListener(OnToggle);
            InteriorDesignerInfoPanelEvents.onPlayerBalanceChanged = (Action<double>)Delegate.Combine(InteriorDesignerInfoPanelEvents.onPlayerBalanceChanged, OnBalance);
            InteriorDesignerInfoPanelEvents.onCostBalanceChanged = (Action<double>)Delegate.Combine(InteriorDesignerInfoPanelEvents.onCostBalanceChanged, OnCost);
            InteriorDesignerInfoPanelEvents.onUpdateInteriorScore = (Action<float>)Delegate.Combine(InteriorDesignerInfoPanelEvents.onUpdateInteriorScore, OnScore);
            InteriorDesignerInfoPanelEvents.onUpdateCustomerCapacity = (Action)Delegate.Combine(InteriorDesignerInfoPanelEvents.onUpdateCustomerCapacity, OnCapacity);
            if (InteriorDesignerUI.IsOpen && !InteriorDesignerHelper.BlueprintCreatorMode && !_opened)
            {
                _opened = true;   // the interface came on while the designer was open: what it shows is measured from here
                Begin();
                HudHints.Keep(true);
            }
        }

        /// <summary>The game's events let go, and every reference to the game's objects with them.</summary>
        public static void Unhook()
        {
            if (_hooked)
            {
                _hooked = false;
                InteriorDesignerUI.OnInteriorDesignerToggle.RemoveListener(OnToggle);
                InteriorDesignerInfoPanelEvents.onPlayerBalanceChanged = (Action<double>)Delegate.Remove(InteriorDesignerInfoPanelEvents.onPlayerBalanceChanged, OnBalance);
                InteriorDesignerInfoPanelEvents.onCostBalanceChanged = (Action<double>)Delegate.Remove(InteriorDesignerInfoPanelEvents.onCostBalanceChanged, OnCost);
                InteriorDesignerInfoPanelEvents.onUpdateInteriorScore = (Action<float>)Delegate.Remove(InteriorDesignerInfoPanelEvents.onUpdateInteriorScore, OnScore);
                InteriorDesignerInfoPanelEvents.onUpdateCustomerCapacity = (Action)Delegate.Remove(InteriorDesignerInfoPanelEvents.onUpdateCustomerCapacity, OnCapacity);
            }
            Forget();
        }

        /// <summary>A new city, or the kit gone: everything held about the game's designer is let go.</summary>
        public static void Forget()
        {
            _ui = null;
            _conceal = null;
            _buttons = new ToolButton[0];
            Extras.Clear();
            _opened = false;
            DesignerPanes.Forget();
            ForgetEdit();
            ForgetCatalogue();
            ForgetMaterials();
            ForgetSheets();
        }

        // The designer has just opened (the game's own event, after its panels read their first state): what the readings are measured against is taken now.
        private static void Toggled(bool open)
        {
            bool creator = InteriorDesignerHelper.BlueprintCreatorMode;
            _opened = open && !creator;
            if (_opened)
            {
                Begin();
                HudHints.Keep(true);
            }
            else
            {
                End();
                HudHints.Keep(false);
            }
            if (open && creator)
            {
                DesignerKits.Classic();   // the blueprint creator's panels are the game's own: the kit steps back until it closes
            }
            Stale?.Invoke();
        }

        private static void Begin()
        {
            _buttons = Toolbar() != null ? Toolbar().GetComponentsInChildren<ToolButton>(true) : new ToolButton[0];
            ReadExtras();
            DesignerPanes.Look(Ui);
            BeginEdit();
            BeginCatalogue();
            BeginMaterials();
            BeginSheets();
            ConcealMore();
        }

        private static void End()
        {
            EndEdit();
            EndCatalogue();
            EndMaterials();
            EndSheets();
        }

        private static void Balance(double balance)
        {
            _balanceAfter = balance;
            Stale?.Invoke();
        }

        private static void Cost(double cost)
        {
            _costBalance = cost;
            Stale?.Invoke();
        }

        private static void Score(float score)
        {
            _score = score;
            Stale?.Invoke();
        }

        private static void Capacity()
        {
            _capacityStale = true;
            Stale?.Invoke();
        }

        // ------------------------------------------------------------------ the tools and the steps on the bar

        /// <summary>The tool in hand as the row's index (another mod's tool after the kit's own), or -1 for none.</summary>
        public static int ToolInHand()
        {
            ToolName now = InteriorDesignerController.CurrentToolName;
            for (int i = 0; i < Tools.Length; i++)
            {
                if (Tools[i] == now)
                {
                    return i;
                }
            }
            for (int i = 0; i < Extras.Count; i++)
            {
                if (Extras[i] != null && Extras[i].toolName == now)
                {
                    return Tools.Length + i;
                }
            }
            return -1;
        }

        // The toolbar's buttons whose tool the kit's row does not know: another mod's. Read as the designer opens, with the buttons.
        private static void ReadExtras()
        {
            Extras.Clear();
            for (int i = 0; i < _buttons.Length; i++)
            {
                if (_buttons[i] != null && Array.IndexOf(Tools, _buttons[i].toolName) < 0 && _buttons[i].toolName != ToolName.None)
                {
                    Extras.Add(_buttons[i]);
                }
            }
        }

        /// <summary>How many tools of other mods the toolbar holds.</summary>
        public static int ExtraCount => Extras.Count;

        /// <summary>One of those tools' names as its button says it (its words, else its object's name).</summary>
        public static string ExtraName(int i)
        {
            ToolButton b = i >= 0 && i < Extras.Count ? Extras[i] : null;
            if (b == null)
            {
                return string.Empty;
            }
            string label = UiWidgets.Label(b);
            return string.IsNullOrWhiteSpace(label) ? b.gameObject.name : label;
        }

        /// <summary>True when the game does not offer that tool here (its button is off).</summary>
        public static bool ExtraOff(int i) => i < 0 || i >= Extras.Count || Extras[i] == null || !Extras[i].gameObject.activeSelf;

        /// <summary>True when the game offers this tool in this business (its toolbar button is there).</summary>
        public static bool Offers(int tool)
        {
            if (tool < 0 || tool >= Tools.Length)
            {
                return false;
            }
            for (int i = 0; i < _buttons.Length; i++)
            {
                if (_buttons[i] != null && _buttons[i].toolName == Tools[tool])
                {
                    return _buttons[i].gameObject.activeSelf;
                }
            }
            return _buttons.Length == 0;
        }

        /// <summary>A tool taken as the game's own button takes it: the tool to come back to after a use forgotten, then the game's ToggleTool, which keeps its own rules.</summary>
        public static bool Tool(int tool)
        {
            InteriorDesignerUI ui = Ui;
            int extra = tool - Tools.Length;
            if (ui == null || !Open || tool < 0 || (extra < 0 ? !Offers(tool) : ExtraOff(extra)))
            {
                return false;
            }
            IInteriorDesignerTool.toolToOpenAfterUsage = ToolName.None;
            IInteriorDesignerTool.toolToOpenAfterUsageArguments = null;
            ui.ToggleTool(extra < 0 ? Tools[tool] : Extras[extra].toolName);
            return true;
        }

        /// <summary>One of the steps: undo, redo, a floor down, a floor up, the walls' look, each the method the game's own button calls.</summary>
        public static bool Step(UiDesignerStep step)
        {
            InteriorDesignerUI ui = Ui;
            if (ui == null || !Open)
            {
                return false;
            }
            switch (step)
            {
                case UiDesignerStep.Undo:
                    if (!ui.HasChanges)
                    {
                        return false;
                    }
                    ui.Undo();
                    return true;
                case UiDesignerStep.Redo:
                    if (!CanRedo())
                    {
                        return false;
                    }
                    ui.Redo();
                    return true;
                case UiDesignerStep.FloorDown:
                    ui.ShowLowerFloor();
                    return true;
                case UiDesignerStep.FloorUp:
                    ui.ShowUpperFloor();
                    return true;
                case UiDesignerStep.Walls:
                    ui.ToggleWalls();
                    return true;
            }
            return false;
        }

        /// <summary>Apply: the game's own question when anything changed, else the designer closes.</summary>
        public static bool Apply()
        {
            InteriorDesignerUI ui = Ui;
            if (ui == null || !Open)
            {
                return false;
            }
            ui.HideWithConfirm();
            return true;
        }

        /// <summary>Close: what the game's close button does, stepping out of a tool, then asking before it discards.</summary>
        public static bool Close()
        {
            InteriorDesignerUI ui = Ui;
            if (ui == null || !Open)
            {
                return false;
            }
            ui.OnCloseButton();
            return true;
        }

        private static bool CanRedo()
        {
            InteriorDesignerUI ui = Ui;
            Button redo = ui != null && GameContract.DesignerRedo.Field != null ? GameContract.DesignerRedo.Field.GetValue(ui) as Button : null;
            return redo != null && redo.interactable;
        }

        // The game switches its floor buttons on only after its toolbar has come on, which is when the kit first lays out, so the buttons' state is the last building's.
        // The game's own condition for them, a building with floors, is read instead; without it the floor index has no controller to ask.
        private static bool HasFloors()
        {
            BuildingManager manager = InstanceBehavior<BuildingManager>.Instance;
            return manager != null && manager.multipleHeightsBuildingController != null;
        }

        /// <summary>The floor in view (0 to 2) while the building has floors, else -1.</summary>
        public static int FloorShown() => HasFloors() ? ChangeFloorRevertibleAction.GetCurrentViewFloorIndex() : -1;
    }
}
