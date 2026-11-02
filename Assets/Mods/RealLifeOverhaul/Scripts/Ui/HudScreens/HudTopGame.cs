using System;
using System.Runtime.CompilerServices;
using Character;
using Dancing;
using UI;
using UI.Topbar;
using UnityEngine;
using UnityEngine.UI;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The game's top bar under the HUD: its leaves hidden by name (the avatar and its two buttons, the accessories' tab, the
    /// three needs, the money, the date, the clock, the pause and the four buttons at the right; the left bar's own picture
    /// by its renderer, as other mods hang their pieces on the bar) and the pause's pulse over the screen, each given back
    /// exactly as found; the top bar's small buttons in a pill of the status bar (the dances, the accessories, the help and,
    /// while the game offers it, the bug report as Going Public leaves it), and the two that open a list, the dances and the
    /// accessories, as short panels over the bar (UiHudPopup) mirroring the game's own: the game's dance list (its buttons
    /// pressed, so it starts the dance its way) and the accessories the save keeps (each switch and each "take off" the
    /// game's own method). Cost: a few reads four times a second while the HUD shows.
    /// </summary>
    internal static class HudTopGame
    {
        public const int Dances = 0, Accessories = 1, Help = 2, Bug = 3;   // the tools in the bar's pill, in this order when all show

        private static readonly string[] LeftLeaves = { "Avatar", "AccessoriesPanel", "PlayerStats", "Splitter", "Money", "MoneyTransaction", "Splitter (1)", "Date", "Time", "TimeControls" };
        private static readonly string[] RightLeaves = { "NotificationsButton", "HelpButton", "ReportBugButton", "SettingsButton" };
        private static readonly int DancingHash = Animator.StringToHash("Dancing");        // the game's own animator parameters (BaseHuman)
        private static readonly int DanceTypeHash = Animator.StringToHash("TypeOfDance");

        private static readonly UiConceal Hidden = new UiConceal();
        private static readonly UiConceal Selector = new UiConceal();   // the dance list's own canvas: unseen, and no longer catching clicks
        private static readonly UiIcon[] Tools = new UiIcon[4];
        private static readonly bool[] ToolsOn = new bool[4];
        private static readonly bool[] ToolsOff = new bool[4];
        private static readonly int[] ToolKind = new int[4];   // which tool each place of the pill holds
        private static readonly Button[] DanceButtons = new Button[16];
        private static readonly string[] DanceNames = new string[16];
        private static readonly bool[] DanceLocked = new bool[16];   // not learned yet (Leisure/DanceLocks): greyed, the dance class named
        private static readonly UiHudPopupModel DancesPopup = new UiHudPopupModel { Numbered = true, Rows = new UiHudPopupRow[16] };
        private static readonly UiHudPopupModel AccessoriesPopup = new UiHudPopupModel { Rows = new UiHudPopupRow[3] };

        private static Button _help, _bug;
        private static PlayerDancesUI _dances;
        private static Transform _danceList;
        private static int _toolCount;
        private static int _danceCount;
        private static bool _built;

        /// <summary>The tools in the bar's pill now, for the status bar's model.</summary>
        public static void Fill(UiStatusModel m)
        {
            m.Tools = Tools;
            m.ToolsOn = ToolsOn;
            m.ToolsOff = ToolsOff;
            m.ToolCount = _toolCount;
        }

        /// <summary>The game's top bar hidden by leaf; false while it is not built yet.</summary>
        public static bool Begin(UIs uis)
        {
            Topbar top = uis != null ? uis.topBar : null;
            Transform left = top != null ? top.transform.Find("Container/Left Bar") : null;
            Transform right = top != null ? top.transform.Find("Container/Buttons") : null;
            if (left == null || right == null)
            {
                return false;
            }
            Hidden.Shared(left);
            Hidden.Shared(right);
            Hidden.Add(left.GetComponent<Graphic>());   // the bar's own picture; its children are hidden one by one
            for (int i = 0; i < left.childCount; i++)
            {
                Transform leaf = left.GetChild(i);
                if (Array.IndexOf(LeftLeaves, leaf.name) >= 0)
                {
                    Hidden.Add(leaf);
                }
            }
            for (int i = 0; i < right.childCount; i++)
            {
                Transform leaf = right.GetChild(i);
                if (Array.IndexOf(RightLeaves, leaf.name) >= 0)
                {
                    Hidden.Add(leaf);
                }
            }
            Transform paused = uis.playerHUD != null ? uis.playerHUD.transform.Find("PausedImage") : null;
            if (paused != null)
            {
                Hidden.Add(paused);   // pause is the time pill's state: the screen's pulse goes
            }
            _help = Child(right, "HelpButton");
            _bug = Child(right, "ReportBugButton");
            _dances = top.playerDancesUI;
            _danceList = _dances != null ? _dances.transform.Find("DanceTypesList") : null;
            if (_dances != null)
            {
                Selector.Add(_dances.transform.Find("Background"));
                if (_danceList != null)
                {
                    Selector.Add(_danceList);
                }
            }
            Hidden.Hide();
            Selector.Hide();
            _built = true;
            return true;
        }

        /// <summary>Everything given back as found.</summary>
        public static void End()
        {
            Hidden.Clear();
            Selector.Clear();
            _built = false;
            _toolCount = 0;
        }

        /// <summary>A new city: the bar is looked for again.</summary>
        public static void Rearm()
        {
            End();
            _help = _bug = null;
            _dances = null;
            _danceList = null;
            Array.Clear(DanceButtons, 0, DanceButtons.Length);
        }

        /// <summary>
        /// The tools and the two lists read again; the signature of what shows. The dance list and the accessories open as
        /// the popups the model is given.
        /// </summary>
        public static int Look(UiHudModel model, UiHudPopupModel[] popups, ref int popupCount)
        {
            if (!_built)
            {
                return 0;
            }
            GameInstance save = SaveGameManager.Current;
            bool dancesOpen = _dances != null && _dances.gameObject.activeSelf;
            bool canDance = Guarded(CanDance);
            bool accessoriesOpen = save != null && save.accessoriesData != null && save.accessoriesData.isPanelOpen;
            _toolCount = 0;
            Tool(Dances, UiIcon.MusicNotes, dancesOpen, !canDance && !dancesOpen);
            Tool(Accessories, UiIcon.Cap, accessoriesOpen, false);
            if (_help != null && _help.gameObject.activeSelf)
            {
                Tool(Help, UiIcon.Info, false, !_help.interactable);
            }
            if (UiWidgets.Shows(_bug))
            {
                Tool(Bug, UiIcon.Bug, false, false);   // only while the game (or Going Public) offers it
            }
            int sig = _toolCount;
            for (int i = 0; i < _toolCount; i++)
            {
                sig = sig * 31 + ToolKind[i] * 4 + (ToolsOn[i] ? 1 : 0) + (ToolsOff[i] ? 2 : 0);
            }
            if (dancesOpen)
            {
                sig = sig * 31 + DancesList(popups, ref popupCount);
            }
            if (accessoriesOpen)
            {
                sig = sig * 31 + AccessoriesList(save.accessoriesData, popups, ref popupCount);
            }
            return sig;
        }

        private static void Tool(int kind, UiIcon icon, bool on, bool off)
        {
            Tools[_toolCount] = icon;
            ToolsOn[_toolCount] = on;
            ToolsOff[_toolCount] = off;
            ToolKind[_toolCount] = kind;
            _toolCount++;
        }

        private static int PlaceOf(int kind)
        {
            for (int i = 0; i < _toolCount; i++)
            {
                if (ToolKind[i] == kind)
                {
                    return i;
                }
            }
            return -1;
        }

        /// <summary>A tool of the bar's pill pressed: the game's own button or method, as its top bar's own does.</summary>
        public static bool PressTool(int index)
        {
            if (index < 0 || index >= _toolCount)
            {
                return false;
            }
            switch (ToolKind[index])
            {
                case Dances:
                    return Guarded(ToggleDances);
                case Accessories:
                    return Guarded(ToggleAccessories);
                case Help:
                    return UiWidgets.Press(_help);
                case Bug:
                    return UiWidgets.Press(_bug);
            }
            return false;
        }

        /// <summary>A popup's row, switch or button pressed (popup is DancesPopup or AccessoriesPopup by reference).</summary>
        public static bool PressPopup(UiHudPopupModel popup, int command, int row)
        {
            if (popup == DancesPopup)
            {
                if (command == UiHudPopup.RowCommand && row >= 0 && row < _danceCount)
                {
                    return UiWidgets.Press(DanceButtons[row]);   // the game's own button: it closes the list and starts the dance its way
                }
                if (command == UiHudPopup.ActionCommand)
                {
                    return Guarded(StopDance);
                }
                return false;
            }
            if (popup == AccessoriesPopup)
            {
                try
                {
                    return Accessory(command, row);
                }
                catch (Exception e) when (Guard.Missing(e))
                {
                    Guard.DoesWithout(e, "the HUD's accessories");
                    return false;
                }
            }
            return false;
        }

        // ------------------------------------------------------------------------------------------------- the dances

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool CanDance() => PlayerDances.IsEnabled;

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool ToggleDances()
        {
            if (_dances == null)
            {
                return false;
            }
            if (_dances.gameObject.activeSelf)
            {
                _dances.gameObject.SetActive(false);   // as the game's cancel key closes it (CancelButtonHandler)
                return true;
            }
            if (!PlayerDances.IsEnabled)
            {
                return false;
            }
            _dances.OnDanceButtonClick();
            return true;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool StopDance()
        {
            if (!IsDancing(out _))
            {
                return false;
            }
            PlayerDances.StopDancing();   // what the game does when the player moves off
            if (_dances != null)
            {
                _dances.gameObject.SetActive(false);
            }
            return true;
        }

        // The player is dancing, and which dance; false on a game without the pieces it reads.
        private static bool IsDancing(out int which)
        {
            try
            {
                return Dancing(out which);
            }
            catch (Exception e) when (Guard.Missing(e))
            {
                Guard.DoesWithout(e, "the HUD's dances");
                which = -1;
                return false;
            }
        }

        // The player is dancing, and which of the game's dances (its index in Dances.GetAllDances), from the player's own
        // animator as the game sets it.
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool Dancing(out int which)
        {
            which = -1;
            PlayerController player = InstanceBehavior<GameManager>.Instance != null ? InstanceBehavior<GameManager>.Instance.playerController : null;
            Animator animator = player != null && player.Character != null ? player.Character.animator : null;
            if (animator == null || !animator.GetBool(DancingHash))
            {
                return false;
            }
            float value = animator.GetFloat(DanceTypeHash);
            DanceType[] all = global::Dancing.Dances.GetAllDances();
            for (int i = 0; i < all.Length; i++)
            {
                if (Math.Abs(all[i].GetValue() - value) < 0.01f)
                {
                    which = i;
                    break;
                }
            }
            return true;
        }

        // The game's dance list as rows: its buttons in its order (the game's every dance), the one danced on the chosen tone.
        private static int DancesList(UiHudPopupModel[] popups, ref int count)
        {
            _danceCount = 0;
            if (_danceList != null)
            {
                for (int i = 0; i < _danceList.childCount && _danceCount < DanceButtons.Length; i++)
                {
                    Transform b = _danceList.GetChild(i);
                    if (!b.gameObject.activeSelf)
                    {
                        continue;   // the template
                    }
                    Button button = b.GetComponent<Button>();
                    Transform label = b.Find("DanceNameLabel");
                    if (button == null || label == null)
                    {
                        continue;   // another mod's own piece in the list: the game's list leaves it to the game
                    }
                    DanceButtons[_danceCount] = button;
                    DanceNames[_danceCount] = UiWidgets.Text(label.GetComponent<TMPro.TMP_Text>());
                    int dance = Leisure.DanceLocks.DanceOf(b);
                    DanceLocked[_danceCount] = Leisure.DanceLocks.Active && dance >= 0 && !Leisure.DanceRules.Known(Leisure.LeisureState.Dances, dance);
                    _danceCount++;
                }
            }
            bool dancing = IsDancing(out int which);
            UiHudPopupModel m = DancesPopup;
            m.Title = UiText.T("reallife_ui_hud_dances");
            m.Meta = Leisure.DanceLocks.Active ? Known() : dancing ? UiText.T("reallife_ui_hud_dancing") : null;   // "4 of 9 known" while some are to learn
            m.Empty = UiText.T("reallife_ui_hud_dances_empty");
            m.Action = dancing ? UiText.T("reallife_ui_hud_dance_stop") : null;
            m.ActionDisabled = false;
            m.RowCount = _danceCount;
            int sig = _danceCount * 7 + which * 3 + (dancing ? 1 : 0) + (m.Meta != null ? m.Meta.GetHashCode() : 0);
            for (int i = 0; i < _danceCount; i++)
            {
                bool now = i == which, locked = DanceLocked[i];
                m.Rows[i] = new UiHudPopupRow
                {
                    Text = DanceNames[i], Press = !locked, Disabled = locked || !DanceButtons[i].interactable,
                    Chip = now ? UiText.T("reallife_ui_hud_dancing") : locked ? UiText.T("reallife_study_course_dance_class") : null,
                    ChipTone = now ? UiRole.Accent : UiRole.None
                };
                sig = sig * 31 + (DanceNames[i] != null ? DanceNames[i].GetHashCode() : 0) + (locked ? 1 : 0) + (m.Rows[i].Disabled ? 2 : 0);
            }
            m.AnchorX = AnchorOf(Dances);
            if (count < popups.Length)
            {
                popups[count++] = m;
            }
            return sig;
        }

        // "4 of 9 known": the dances learned of the game's nine.
        private static string Known()
        {
            int known = Leisure.DanceRules.KnownCount(Leisure.LeisureState.Dances);
            return UiText.T("reallife_ui_hud_dances_known").Replace("{known}", known.ToString()).Replace("{count}", Leisure.DanceRules.Count.ToString());
        }

        // ------------------------------------------------------------------------------------------------- the accessories

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool ToggleAccessories()
        {
            UIs uis = InstanceBehavior<UIs>.Instance;
            if (uis == null || uis.topBar == null || uis.topBar.accessoriesUI == null)
            {
                return false;
            }
            uis.topBar.accessoriesUI.OnAccessoriesButtonClick();
            return true;
        }

        // The three slots the save keeps (hand, head, phone): each worn accessory with its slot, a switch for the two the
        // game lets the player hide, and the button that takes all off through the game's own unequip, slot by slot.
        private static int AccessoriesList(UI.Topbar.Accessories.AccessoriesData data, UiHudPopupModel[] popups, ref int count)
        {
            UiHudPopupModel m = AccessoriesPopup;
            m.Title = UiText.T("reallife_ui_hud_accessories");
            m.Empty = UiText.T("reallife_ui_hud_accessories_empty");
            m.RowCount = 0;
            int worn = 0, sig = 0;
            Slot(m, data.handAccessoryCargoInstance, "reallife_ui_hud_slot_hand", UiIcon.Briefcase, true, data.handAccessoryVisible, ref worn, ref sig);
            Slot(m, data.headAccessoryCargoInstance, "reallife_ui_hud_slot_head", UiIcon.Cap, true, data.headAccessoryVisible, ref worn, ref sig);
            Slot(m, data.phoneAccessoryCargoInstance, "reallife_ui_hud_slot_phone", UiIcon.Headset, false, true, ref worn, ref sig);
            int shown = 0;
            for (int i = 0; i < m.RowCount; i++)
            {
                shown += !m.Rows[i].HasSwitch || m.Rows[i].On ? 1 : 0;   // the phone's has no switch: always worn in sight
            }
            m.Meta = worn > 0 ? UiText.T("reallife_ui_hud_accessories_shown").Replace("{shown}", shown.ToString()).Replace("{count}", worn.ToString()) : null;
            m.Action = worn > 0 ? UiText.T("reallife_ui_hud_accessories_off") : null;
            m.ActionDisabled = false;
            m.AnchorX = AnchorOf(Accessories);
            if (count < popups.Length)
            {
                popups[count++] = m;
            }
            return sig * 31 + worn;
        }

        private static readonly int[] SlotOf = new int[3];   // each row's slot: 0 hand, 1 head, 2 phone

        private static void Slot(UiHudPopupModel m, BigAmbitions.Items.CargoInstance cargo, string slotKey, UiIcon icon, bool canHide, bool visible, ref int worn, ref int sig)
        {
            int slot = slotKey == "reallife_ui_hud_slot_hand" ? 0 : slotKey == "reallife_ui_hud_slot_head" ? 1 : 2;
            if (cargo == null || string.IsNullOrEmpty(cargo.itemName))
            {
                return;
            }
            worn++;
            string name = UiText.T(cargo.itemName);
            SlotOf[m.RowCount] = slot;
            m.Rows[m.RowCount++] = new UiHudPopupRow { Icon = icon, Text = name, Sub = UiText.T(slotKey), HasSwitch = canHide, On = visible };
            sig = sig * 31 + slot * 4 + (visible ? 1 : 0) + name.GetHashCode();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool Accessory(int command, int row)
        {
            UIs uis = InstanceBehavior<UIs>.Instance;
            var ui = uis != null && uis.topBar != null ? uis.topBar.accessoriesUI : null;
            if (ui == null)
            {
                return false;
            }
            if (command == UiHudPopup.SwitchCommand && row >= 0 && row < AccessoriesPopup.RowCount)
            {
                if (SlotOf[row] == 0)
                {
                    ui.OnHandAccessoryVisibleButtonClick();
                }
                else if (SlotOf[row] == 1)
                {
                    ui.OnHeadAccessoryVisibleButtonClick();
                }
                else
                {
                    return false;
                }
                return true;
            }
            if (command == UiHudPopup.ActionCommand)
            {
                // Each slot through its own button's method, as a click on each slot of the game's tab takes that one off.
                ui.OnPhoneAccessoryButtonClick();
                ui.OnHeadAccessoryButtonClick();
                ui.OnHandAccessoryButtonClick();
                return true;
            }
            return false;
        }

        // ------------------------------------------------------------------------------------------------- shared

        private static float AnchorOf(int kind)
        {
            int at = PlaceOf(kind);
            UiStatusModel bar = HudLive.LastBar;
            if (at < 0 || bar == null)
            {
                return UiHud.Left + UiHudPopup.Width / 2f;
            }
            return UiDraw.ToolMiddle(UiDraw.PlanPills(bar, HudLive.FrameWidth), at);
        }

        private static Button Child(Transform parent, string name)
        {
            Transform t = parent != null ? parent.Find(name) : null;
            return t != null ? t.GetComponent<Button>() : null;
        }

        private static bool Guarded(Func<bool> read)
        {
            try
            {
                return read();
            }
            catch (Exception e) when (Guard.Missing(e))
            {
                Guard.DoesWithout(e, "the HUD's dances and accessories");
                return false;
            }
        }
    }
}
