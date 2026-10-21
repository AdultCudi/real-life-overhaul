using System;
using System.Runtime.CompilerServices;
using BigAmbitions.InputSystem;
using UI;
using UI.Smartphone;
using UnityEngine;
using UnityEngine.EventSystems;

namespace RealLifeOverhaul.GameUi
{
    /// <summary>
    /// On the HUD phone's grid, switched on only while one of its folders is open (PhoneGridPager.Keys): the game's cancel
    /// key goes back to the home screen, as the game's own windows close on it, and the press is taken so that the game's
    /// menu does not open as well (PlayerAction.Reset, as TravelListKeys takes it). Only while nothing the game's key would
    /// close first is up: a question, the game's menu, the full menu, the map, the options, a panel of an activity, a
    /// dialogue, a building's card, a preview or the interior designer, a list window of the mod's, or a field being typed
    /// in, and while the phone is folded away; then the key is left to the game. It runs before the game's own key handling.
    ///
    /// Cost: nothing while no folder is open (switched off); while one is, one look at the key a frame.
    /// </summary>
    [DefaultExecutionOrder(-31000)]
    public sealed class PhoneFolderKeys : MonoBehaviour
    {
        private static readonly FeatureGuard FrameGuard = Guard.For(null, "PhoneFolderKeys", typeof(PhoneFolderKeys));   // its Update, LateUpdate and FixedUpdate (Core/Guard)
        private static readonly int HotUpdate = HotSpots.Slot("Core: PhoneFolderKeys.Update");

        internal PhoneGridPager Pager;
        private static bool _without;   // this game lacks a member the check needs: the key is left to the game, the Back tile still works

        private void Update()
        {
            using var hotSpot = HotSpots.Time(HotUpdate);
            if (FrameGuard.Off)
            {
                enabled = false;   // (its feature was switched off after its faults: Core/Guard)
                return;
            }
            try
            {
                OnUpdate();
            }
            catch (Exception e)
            {
                Guard.Fail(FrameGuard, e, this);
            }
        }

        private void OnUpdate()
        {
            if (Pager == null || Pager.Folder < 0)
            {
                enabled = false;
                return;
            }
            if (!PlayerAction.Cancel.Pressed() || !KeyIsFree())
            {
                return;
            }
            PlayerAction.Cancel.Reset();   // taken, so the game's own handling does not open its menu too
            Pager.CloseFolder();
        }

        // Whether nothing of the game's that the key closes first is up (the order of the game's CancelButtonHandler).
        private static bool KeyIsFree()
        {
            if (_without || TravelList.AnyOpen)
            {
                return false;
            }
            try
            {
                return GameLeavesKey();
            }
            catch (Exception e) when (Guard.Missing(e))
            {
                _without = true;
                Guard.DoesWithout(e, "the cancel key closing a folder of the phone", "its Back tile closes it");
                return false;
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool GameLeavesKey()
        {
            if (GameManager.HasInputSelected() || HudConfirm.isOpen || UI.MiniMenu.MiniMenu.IsOpen || FullMenu.IsOpen || CityMap.IsOpen || Scenes.MainMenu.Options.IsVisible
                || PlayerActivity.PlayerActivityUI.IsPanelOpen || BuildingPreview.isPreviewing || UI.InteriorDesigner.InteriorDesignerUI.IsOpen)
            {
                return false;
            }
            UIs uis = InstanceBehavior<UIs>.Instance;
            if (uis == null)
            {
                return false;
            }
            if (uis.smartphoneCollapsibleWindow != null && uis.smartphoneCollapsibleWindow.IsCollapsed)
            {
                return false;   // (the phone folded away: the key is the game's, the folder stays as it was)
            }
            if (uis.buildingResume != null && GameMembers.TargetOf(uis.buildingResume) != null)
            {
                return false;
            }
            return uis.playerHUD == null || uis.playerHUD.dialogUI == null || !uis.playerHUD.dialogUI.isPanelOpen;
        }
    }

    /// <summary>
    /// On each of the mod's app buttons on the HUD phone, while its home screen has folders: a right click on the button
    /// opens the list to move the app (PhoneMove), into another folder, onto the home screen or into the dock, as a long
    /// press does on a real phone. A left click is the button's own. It has no Update: it costs nothing a frame.
    /// </summary>
    public sealed class PhoneAppPress : MonoBehaviour, IPointerClickHandler
    {
        private static readonly FeatureGuard FrameGuard = Guard.For(null, "PhoneAppPress", typeof(PhoneAppPress));   // the game's calls of it (Core/Guard)

        internal PhoneGridPager Pager;

        public void OnPointerClick(PointerEventData eventData)
        {
            if (FrameGuard.Off || eventData == null || eventData.button != PointerEventData.InputButton.Right)
            {
                return;
            }
            try
            {
                if (Pager != null && Pager.Foldered)
                {
                    PhoneMove.Open(transform, Pager);
                }
            }
            catch (Exception fault)
            {
                Guard.Fail(FrameGuard, fault);
            }
        }
    }
}
